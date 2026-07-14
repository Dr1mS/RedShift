using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Caméra drone du spectateur (SPEC §4.11) : à la mort, la tête (caméra locale,
    /// non répliquée) est détachée du corps et vole librement jusqu'au saut.
    /// Purement local au propriétaire — le cadavre reste en place pour les autres.
    /// Activé par <see cref="PlayerHealth"/> ; désactivé par défaut.
    /// </summary>
    public class SpectatorController : MonoBehaviour
    {
        [SerializeField] private PlayerHealthDef _def;
        [SerializeField, Tooltip("Rig caméra à libérer : la tête.")]
        private Transform _rig;
        [SerializeField] private InputActionAsset _inputAsset;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction upAction;
        private InputAction downAction;
        private InputAction fastAction;

        private Vector3 anchoredUp = Vector3.up;
        private float yaw;
        private float pitch;

        /// <summary>Appelé par PlayerHealth à la mort du propriétaire.</summary>
        public void Begin()
        {
            InputActionMap map = _inputAsset.FindActionMap("Player", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            lookAction = map.FindAction("Look", throwIfNotFound: true);
            upAction = map.FindAction("Jump", throwIfNotFound: true);
            downAction = map.FindAction("Crouch", throwIfNotFound: true);
            fastAction = map.FindAction("Sprint", throwIfNotFound: true);
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

            enabled = true;
        }

        private void Update()
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
    }
}
