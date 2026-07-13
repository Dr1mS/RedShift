using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Lampe frontale avec batterie. L'état on/off est répliqué (les autres joueurs
    /// voient la lumière — et la Nuée la verra en P4). La batterie est simulée
    /// côté propriétaire uniquement (tunables dans LampDef).
    /// </summary>
    public class HeadLamp : NetworkBehaviour
    {
        [SerializeField] private LampDef _def;
        [SerializeField] private Light _light;
        [SerializeField] private InputActionAsset _inputAsset;

        private readonly SyncVar<bool> _isOn = new();

        private InputAction lampAction;
        private float battery;

        public bool IsOn => _isOn.Value;
        public float BatteryNormalized => _def == null ? 1f : battery / _def.BatteryDuration;

        private void Awake()
        {
            _isOn.OnChange += (_, next, _) => _light.enabled = next;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _light.range = _def.Range;
            _light.spotAngle = _def.SpotAngle;
            _light.intensity = _def.Intensity;
            _light.enabled = _isOn.Value;

            if (!IsOwner)
                return;
            battery = _def.BatteryDuration;
            lampAction = _inputAsset.FindActionMap("Player", throwIfNotFound: true).FindAction("Lamp", throwIfNotFound: true);
        }

        private void Update()
        {
            if (!IsOwner || lampAction == null)
                return;

            if (lampAction.WasPressedThisFrame() && (battery > 0f || _isOn.Value))
                SetLampServerRpc(!_isOn.Value);

            if (_isOn.Value)
            {
                battery -= Time.deltaTime;
                if (battery <= 0f)
                {
                    battery = 0f;
                    SetLampServerRpc(false);
                }
            }
        }

        [ServerRpc]
        private void SetLampServerRpc(bool on) => _isOn.Value = on;
    }
}
