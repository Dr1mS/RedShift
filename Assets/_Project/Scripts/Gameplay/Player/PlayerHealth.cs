using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Santé/mort du joueur (SPEC §4.11) — v1 P3 : mort binaire (onde de choc, monstres).
    /// L'objet en main est lâché côté serveur, le motor est gelé partout.
    /// Spectateur libre + corps lootable + respawn : complétés en P3-7.
    /// </summary>
    public class PlayerHealth : NetworkBehaviour
    {
        [SerializeField] private PlayerHealthDef _def;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerInventory _inventory;

        private readonly SyncVar<bool> _dead = new();
        private readonly SyncVar<float> _health = new();

        public bool IsDead => _dead.Value;
        public float HealthNormalized => _def == null || _def.MaxHealth <= 0f ? 1f : _health.Value / _def.MaxHealth;

        private void Awake()
        {
            _dead.OnChange += OnDeadChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _health.Value = _def != null ? _def.MaxHealth : 100f;
        }

        /// <summary>Serveur : dégâts (monstres, chutes futures). À zéro : mort.</summary>
        [Server]
        public void ApplyDamage(float damage)
        {
            if (_dead.Value || damage <= 0f)
                return;
            _health.Value = Mathf.Max(0f, _health.Value - damage);
            if (_health.Value <= 0f)
                Kill();
        }

        [Server]
        public void Kill()
        {
            if (_dead.Value)
                return;
            _health.Value = 0f;
            if (_inventory != null)
                _inventory.ServerDropHand();
            _dead.Value = true;
        }

        private void OnDeadChanged(bool prev, bool next, bool asServer)
        {
            // Le host reçoit les deux passes : n'appliquer l'état qu'une fois.
            if (asServer && IsClientInitialized)
                return;
            if (next)
                _motor.SetSeated(true, false); // gel v1 (caméra libre en P3-7)
        }
    }
}
