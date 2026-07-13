using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Pitch caméra local (le yaw est appliqué au corps par <see cref="PlayerMotor"/>).
    /// Activé uniquement chez le propriétaire.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] private PlayerMovementDef _config;
        [SerializeField] private InputActionAsset _inputAsset;

        private InputAction lookAction;
        private float pitch;

        private void OnEnable()
        {
            lookAction = _inputAsset.FindActionMap("Player", throwIfNotFound: true).FindAction("Look", throwIfNotFound: true);
        }

        private void LateUpdate()
        {
            pitch -= lookAction.ReadValue<Vector2>().y * _config.LookSensitivity;
            pitch = Mathf.Clamp(pitch, _config.PitchMin, _config.PitchMax);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
