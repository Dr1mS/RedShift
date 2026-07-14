using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Siège de navette (SPEC §4.3) : le joueur assis suit l'ancre du siège, son controller gelé.
    /// PIÈGE CONNU : pour le siège pilote, l'ownership de la navette est transférée au pilote
    /// AVANT de publier l'état occupant — sinon fight d'autorité.
    ///
    /// P4-5 — pas de parentage réseau : le NetworkTransform du joueur est client-authoritative
    /// et émet en espace local de SON parent. Un SetParent fait côté serveur seul ne se réplique
    /// pas (NT _synchronizeParent désactivé) et créerait une asymétrie d'espaces de coordonnées
    /// (owner sans parent → émet du monde ; host avec parent → interprète en local siège).
    /// À la place, l'owner assis recale chaque frame son corps sur l'ancre (owner-follow) ;
    /// le NT propage naturellement ce suivi à tous les pairs, dans le même espace monde.
    /// </summary>
    public class Seat : NetworkBehaviour, IInteractable
    {
        [SerializeField] private bool _isPilot;
        [SerializeField, Tooltip("Position du joueur assis.")]
        private Transform _anchor;
        [SerializeField, Tooltip("Position de sortie (côté navette).")]
        private Transform _exitPoint;
        [SerializeField] private ShuttleController _shuttle;
        [SerializeField] private InputActionAsset _inputAsset;

        private readonly SyncVar<NetworkObject> _occupant = new();

        private InputAction interactAction;
        private float localSeatTime;
        // Suivi local : la passe client du SyncVar peut livrer un `prev` déjà null,
        // on reconstitue donc la transition nous-mêmes.
        private NetworkObject lastKnownOccupant;
        // Rigidbody de l'occupant local (owner-follow) — mis en cache à l'assise.
        private Rigidbody occupantBody;

        public bool IsOccupied => _occupant.Value != null;
        public bool IsPilotSeat => _isPilot;
        public string Prompt => _isPilot ? "S'asseoir (pilote)" : "S'asseoir (passager)";

        private void Awake()
        {
            _occupant.OnChange += OnOccupantChanged;
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            var hull = _shuttle.GetComponent<ShuttleHull>();
            return !IsOccupied && (hull == null || !hull.IsDestroyed);
        }

        public void Interact(PlayerInteractor interactor)
        {
            var player = interactor.GetComponentInParent<NetworkObject>();
            if (player != null)
                SitServerRpc(player);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SitServerRpc(NetworkObject player, NetworkConnection sender = null)
        {
            if (player == null || IsOccupied || player.Owner != sender)
                return;
            var hull = _shuttle.GetComponent<ShuttleHull>();
            if (hull != null && hull.IsDestroyed)
                return;
            // Garde anti double-assise : un joueur déjà assis sur un siège de la navette
            // ne peut pas en occuper un second (client modifié / course réseau).
            foreach (Seat seat in _shuttle.GetComponentsInChildren<Seat>())
                if (seat._occupant.Value == player)
                    return;

            // Ownership de la navette au pilote AVANT de publier l'occupant (piège connu SPEC) :
            // quand le SyncVar arrive chez le pilote, il est déjà controller de la navette.
            if (_isPilot)
                _shuttle.NetworkObject.GiveOwnership(sender);

            // Pas de SetParent réseau (voir en-tête de classe) : le suivi est fait par
            // l'owner-follow de LateUpdate, répliqué via le NetworkTransform du joueur.
            _occupant.Value = player;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ExitServerRpc(NetworkConnection sender = null)
        {
            NetworkObject player = _occupant.Value;
            if (player == null || player.Owner != sender)
                return;
            ServerUnseat(player);
        }

        [Server]
        private void ServerUnseat(NetworkObject player)
        {
            if (_isPilot)
                _shuttle.NetworkObject.RemoveOwnership();
            _occupant.Value = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Libération du siège à la déconnexion de l'occupant. Invoqué par FishNet AVANT
            // le despawn des objets de la connexion : le RemoveOwnership de ServerUnseat
            // retire aussi la navette de connection.Objects — sinon FishNet la DESPAWNERAIT
            // avec les objets du pilote déconnecté (ServerObjects.ClientDisconnected).
            ServerManager.Objects.OnPreDestroyClientObjects += ServerOnPreDestroyClientObjects;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (ServerManager != null)
                ServerManager.Objects.OnPreDestroyClientObjects -= ServerOnPreDestroyClientObjects;
        }

        private void ServerOnPreDestroyClientObjects(NetworkConnection conn)
        {
            NetworkObject occupant = _occupant.Value;
            if (occupant != null && occupant.Owner == conn)
                ServerUnseat(occupant);
        }

        /// <summary>Destruction de la coque : éjecte l'occupant avec une impulsion (ragdoll en P3).</summary>
        [Server]
        public void ServerEject(float ejectionSpeed)
        {
            NetworkObject player = _occupant.Value;
            if (player == null)
                return;
            ServerUnseat(player);
            EjectTargetRpc(player.Owner, player, ejectionSpeed);
        }

        [TargetRpc]
        private void EjectTargetRpc(NetworkConnection conn, NetworkObject player, float ejectionSpeed)
        {
            var body = player.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
                body.linearVelocity = (transform.up * 2f + Random.insideUnitSphere).normalized * ejectionSpeed;
        }

        private void Update()
        {
            NetworkObject occupant = _occupant.Value;
            if (occupant == null || !occupant.IsOwner)
                return;

            // Anti double-déclenchement : le E qui assoit ne doit pas ressortir le même instant.
            if (Time.time - localSeatTime < 0.4f)
                return;

            interactAction ??= _inputAsset.FindActionMap("Player", throwIfNotFound: true).FindAction("Interact", throwIfNotFound: true);
            if (interactAction.WasPressedThisFrame())
                ExitServerRpc();
        }

        private void LateUpdate()
        {
            // Owner-follow (voir en-tête de classe) : l'owner assis recale son corps sur
            // l'ancre — position ET rotation monde (la navette tourne/s'aligne gravité).
            // Le NetworkTransform client-auth du joueur réplique ce suivi chez les pairs.
            NetworkObject occupant = _occupant.Value;
            if (occupant == null || !occupant.IsOwner)
                return;

            occupant.transform.SetPositionAndRotation(_anchor.position, _anchor.rotation);
            if (occupantBody == null)
                occupantBody = occupant.GetComponent<Rigidbody>();
            // Corps kinématique pendant l'assise : poser aussi la pose physique pour
            // éviter tout fight transform/physics.
            if (occupantBody != null && occupantBody.isKinematic)
            {
                occupantBody.position = _anchor.position;
                occupantBody.rotation = _anchor.rotation;
            }
        }

        private void OnOccupantChanged(NetworkObject prev, NetworkObject next, bool asServer)
        {
            // Le host reçoit aussi la passe client ; on n'exécute la logique visuelle/état qu'une fois.
            if (asServer && IsClientInitialized)
                return;
            if (lastKnownOccupant == next)
                return;

            if (lastKnownOccupant != null)
                HandleUnseat(lastKnownOccupant);
            lastKnownOccupant = next;
            if (next != null)
                HandleSeat(next);
        }

        private void HandleSeat(NetworkObject player)
        {
            // Le joueur peut déjà être détruit (déconnexion : le despawn peut précéder le SyncVar).
            if (player == null)
                return;
            var motor = player.GetComponent<PlayerMotor>();
            if (motor != null)
                motor.SetSeated(true, _isPilot);

            if (player.IsOwner)
            {
                localSeatTime = Time.time;
                player.transform.SetPositionAndRotation(_anchor.position, _anchor.rotation);
                occupantBody = player.GetComponent<Rigidbody>();
                if (_isPilot)
                    _shuttle.SetLocalPilot(true);
            }
        }

        private void HandleUnseat(NetworkObject player)
        {
            occupantBody = null;
            // Le joueur peut déjà être détruit (déconnexion : le despawn peut précéder le SyncVar).
            if (player == null)
                return;
            var motor = player.GetComponent<PlayerMotor>();
            if (motor != null)
                motor.SetSeated(false, _isPilot);

            if (player.IsOwner)
            {
                player.transform.SetPositionAndRotation(_exitPoint.position, _exitPoint.rotation);
                if (_isPilot)
                    _shuttle.SetLocalPilot(false);
            }
        }
    }
}
