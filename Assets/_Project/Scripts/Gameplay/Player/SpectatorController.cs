using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Caméra du spectateur (SPEC §4.11) : à la mort, la tête (caméra locale, non répliquée)
    /// est détachée du corps. Deux modes, bascule à la volée :
    /// <list type="bullet">
    /// <item><b>Libre</b> (drone) : vol 6DOF au clavier/souris jusqu'au saut.</item>
    /// <item><b>Suivi</b> (3e personne greybox) : cycle entre les coéquipiers VIVANTS,
    /// caméra derrière/au-dessus, up aligné sur la gravité locale de la cible (slerp doux,
    /// jamais de snap — piège caméra du projet).</item>
    /// </list>
    /// Purement local au propriétaire mort — aucune réplication : le cadavre reste en place
    /// pour les autres, et les positions/gravités des cibles sont déjà synchronisées ailleurs.
    /// Activé par <see cref="PlayerHealth"/> ; désactivé par défaut.
    /// </summary>
    public class SpectatorController : MonoBehaviour
    {
        /// <summary>Mode courant de la caméra spectateur.</summary>
        public enum Mode { Free, Follow }

        [SerializeField] private PlayerHealthDef _def;
        [SerializeField, Tooltip("Rig caméra à libérer : la tête.")]
        private Transform _rig;
        [SerializeField] private InputActionAsset _inputAsset;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction upAction;
        private InputAction downAction;
        private InputAction fastAction;
        private InputAction nextAction;
        private InputAction prevAction;
        private InputAction freeAction;

        private Vector3 anchoredUp = Vector3.up;
        private float yaw;
        private float pitch;

        private PlayerHealth self;
        private Mode mode = Mode.Free;
        private PlayerHealth followed;
        private int followedClientId = SpectatorTargeting.NoTarget;
        private bool followPoseInitialized;
        // Dernier up de gravité de la cible suivie : sert d'up de reprise en caméra libre,
        // même si la cible a disparu (sinon on garderait l'up figé au moment de la mort).
        private Vector3 lastFollowUp = Vector3.up;

        // Modèle de sélection réutilisé chaque frame (évite les allocations).
        private readonly List<PlayerHealth> aliveCandidates = new();
        private readonly List<int> aliveClientIds = new();

        /// <summary>Mode courant (lu par le HUD).</summary>
        public Mode CurrentMode => mode;

        /// <summary>ClientId du coéquipier suivi, ou -1 en caméra libre (lu par le HUD).</summary>
        public int FollowedClientId => mode == Mode.Follow ? followedClientId : SpectatorTargeting.NoTarget;

        /// <summary>Appelé par PlayerHealth à la mort du propriétaire.</summary>
        public void Begin()
        {
            self = GetComponent<PlayerHealth>();

            InputActionMap map = _inputAsset.FindActionMap("Player", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            lookAction = map.FindAction("Look", throwIfNotFound: true);
            upAction = map.FindAction("Jump", throwIfNotFound: true);
            downAction = map.FindAction("Crouch", throwIfNotFound: true);
            fastAction = map.FindAction("Sprint", throwIfNotFound: true);
            nextAction = map.FindAction("SpectatorNext", throwIfNotFound: true);
            prevAction = map.FindAction("SpectatorPrev", throwIfNotFound: true);
            freeAction = map.FindAction("SpectatorFree", throwIfNotFound: true);
            map.Enable();

            // La caméra part en drone ; l'up de référence est la gravité au moment de la mort.
            var gravity = GetComponent<GravityReceiver>();
            anchoredUp = gravity != null && gravity.InGravity ? gravity.Up : _rig.up;
            _rig.SetParent(null, true);
            _rig.position += anchoredUp * 1.5f;

            Quaternion inverse = Quaternion.Inverse(Quaternion.FromToRotation(Vector3.up, anchoredUp));
            Vector3 localEuler = (inverse * _rig.rotation).eulerAngles;
            yaw = localEuler.y;
            pitch = 0f;

            mode = Mode.Free;
            followed = null;
            followedClientId = SpectatorTargeting.NoTarget;

            enabled = true;
        }

        private void Update()
        {
            HandleModeInput();

            if (mode == Mode.Follow)
                UpdateFollow();
            else
                UpdateFree();
        }

        /// <summary>Bascule libre ↔ suivi et cycle de cible (Tab/V = suivant/précédent, C = libre).</summary>
        private void HandleModeInput()
        {
            if (freeAction.WasPressedThisFrame())
            {
                EnterFreeMode();
                return;
            }

            bool next = nextAction.WasPressedThisFrame();
            bool prev = prevAction.WasPressedThisFrame();
            if (next || prev)
                CycleTarget(forward: next);
        }

        private void EnterFreeMode()
        {
            if (mode == Mode.Free)
                return;
            // Reprendre le vol libre depuis la pose courante de la caméra de suivi (continuité) :
            // up = celui de la cible (dernier connu si elle vient de disparaître), pas celui figé
            // au moment de la mort.
            anchoredUp = followed != null ? TargetUp(followed) : lastFollowUp;
            AnchorFreeOrientationFromRig();
            mode = Mode.Free;
            followed = null;
            followedClientId = SpectatorTargeting.NoTarget;
        }

        /// <summary>Cycle vers le coéquipier vivant suivant/précédent ; retombe en libre si aucun.</summary>
        private void CycleTarget(bool forward)
        {
            RefreshAliveCandidates();
            int nextId = SpectatorTargeting.NextClientId(aliveClientIds, followedClientId, forward);
            if (nextId == SpectatorTargeting.NoTarget)
            {
                EnterFreeMode();
                return;
            }
            SetFollowed(nextId);
        }

        private void SetFollowed(int clientId)
        {
            followed = FindCandidate(clientId);
            if (followed == null)
            {
                EnterFreeMode();
                return;
            }
            followedClientId = clientId;
            mode = Mode.Follow;
            followPoseInitialized = false; // snap la position derrière la nouvelle cible (pas de traversée de carte)
        }

        // --- Mode suivi (3e personne) ---

        private void UpdateFollow()
        {
            // La cible peut avoir disparu (mort, évacuation, déconnexion) depuis la dernière frame.
            RefreshAliveCandidates();
            if (followed == null || !SpectatorTargeting.IsStillValid(aliveClientIds, followedClientId))
            {
                // Bascule automatique vers le vivant suivant, sinon caméra libre.
                int fallback = SpectatorTargeting.NextClientId(aliveClientIds, followedClientId, forward: true);
                if (fallback == SpectatorTargeting.NoTarget)
                {
                    EnterFreeMode();
                    return;
                }
                SetFollowed(fallback);
                if (mode != Mode.Follow)
                    return;
            }

            Transform target = followed.transform;
            Vector3 up = TargetUp(followed);
            lastFollowUp = up; // mémorisé pour une reprise propre en caméra libre si la cible disparaît

            // Base horizontale : la « face » du corps projetée sur le plan de gravité de la cible.
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, up);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(target.up, up);
            forward.Normalize();

            Vector3 lookAtPoint = target.position + up * _def.FollowLookAtHeight;
            Vector3 desiredPos = target.position + up * _def.FollowHeight - forward * _def.FollowDistance;
            Quaternion desiredRot = Quaternion.LookRotation(lookAtPoint - desiredPos, up);

            if (!followPoseInitialized)
            {
                // Changement de cible : snap la POSITION (sinon on traverse la carte) ; l'up/orientation
                // suivra en slerp dès la frame suivante.
                _rig.SetPositionAndRotation(desiredPos, desiredRot);
                followPoseInitialized = true;
                return;
            }

            // Position lissée quand la cible bouge ; orientation/up en slerp (jamais de snap sur l'up).
            float tp = 1f - Mathf.Exp(-_def.FollowPositionSharpness * Time.deltaTime);
            float tr = 1f - Mathf.Exp(-_def.FollowRotationSharpness * Time.deltaTime);
            _rig.position = Vector3.Lerp(_rig.position, desiredPos, tp);
            _rig.rotation = Quaternion.Slerp(_rig.rotation, desiredRot, tr);
        }

        private static Vector3 TargetUp(PlayerHealth player)
        {
            var gravity = player.GetComponent<GravityReceiver>();
            return gravity != null ? gravity.Up : player.transform.up;
        }

        // --- Mode libre (drone) ---

        private void UpdateFree()
        {
            Vector2 look = lookAction.ReadValue<Vector2>() * _def.SpectatorLookSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
            _rig.rotation = Quaternion.FromToRotation(Vector3.up, anchoredUp) * Quaternion.Euler(pitch, yaw, 0f);

            Vector2 move = moveAction.ReadValue<Vector2>();
            float vertical = (upAction.IsPressed() ? 1f : 0f) - (downAction.IsPressed() ? 1f : 0f);
            float speed = _def.SpectatorSpeed * (fastAction.IsPressed() ? _def.SpectatorFastMultiplier : 1f);
            Vector3 velocity = _rig.forward * move.y + _rig.right * move.x + anchoredUp * vertical;
            _rig.position += velocity * (speed * Time.deltaTime);
        }

        /// <summary>Recale yaw/pitch libres à partir de l'orientation courante du rig et de l'up ancré.</summary>
        private void AnchorFreeOrientationFromRig()
        {
            Quaternion inverse = Quaternion.Inverse(Quaternion.FromToRotation(Vector3.up, anchoredUp));
            Vector3 localEuler = (inverse * _rig.rotation).eulerAngles;
            yaw = localEuler.y;
            pitch = Mathf.Clamp(NormalizePitch(localEuler.x), -89f, 89f);
        }

        private static float NormalizePitch(float euler)
            => euler > 180f ? euler - 360f : euler;

        // --- Sélection des cibles vivantes ---

        /// <summary>
        /// Reconstruit la liste des coéquipiers ciblables : vivants (!IsDead), non évacués,
        /// et pas soi-même — triés croissants par ClientId (identité stable pour le cycle).
        /// </summary>
        private void RefreshAliveCandidates()
        {
            aliveCandidates.Clear();
            aliveClientIds.Clear();

            foreach (PlayerHealth p in FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude))
            {
                if (p == self || p == null || p.IsDead || p.IsEvacuated)
                    continue;
                aliveCandidates.Add(p);
            }
            aliveCandidates.Sort(static (a, b) => a.OwnerId.CompareTo(b.OwnerId));
            foreach (PlayerHealth p in aliveCandidates)
                aliveClientIds.Add(p.OwnerId);

            // Resynchronise la référence suivie avec la liste courante (la cible peut avoir changé d'objet).
            if (mode == Mode.Follow && followedClientId != SpectatorTargeting.NoTarget)
                followed = FindCandidate(followedClientId);
        }

        private PlayerHealth FindCandidate(int clientId)
        {
            foreach (PlayerHealth p in aliveCandidates)
                if (p.OwnerId == clientId)
                    return p;
            return null;
        }
    }
}
