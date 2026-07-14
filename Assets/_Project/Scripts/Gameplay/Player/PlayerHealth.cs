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
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerInventory _inventory;

        private readonly SyncVar<bool> _dead = new();

        public bool IsDead => _dead.Value;

        private void Awake()
        {
            _dead.OnChange += OnDeadChanged;
        }

        [Server]
        public void Kill()
        {
            if (_dead.Value)
                return;
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
