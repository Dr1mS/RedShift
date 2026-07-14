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
        [SerializeField, Tooltip("Caméra drone du propriétaire à la mort (SPEC §4.11).")]
        private SpectatorController _spectator;
        [SerializeField, Tooltip("Visée tête à couper à la mort (le spectateur gère la sienne).")]
        private PlayerLook _look;
        [SerializeField, Tooltip("Renderers assombris à la mort (cadavre visible par tous).")]
        private Renderer[] _corpseRenderers;

        private readonly SyncVar<bool> _dead = new();
        private readonly SyncVar<float> _health = new();
        private readonly SyncVar<bool> _evacuated = new();

        public bool IsDead => _dead.Value;
        /// <summary>Parti avec l'Arche au saut : hors du monde, ignoré par l'onde et les monstres.</summary>
        public bool IsEvacuated => _evacuated.Value;
        public float HealthNormalized => _def == null || _def.MaxHealth <= 0f ? 1f : _health.Value / _def.MaxHealth;

        private void Awake()
        {
            _dead.OnChange += OnDeadChanged;
            _evacuated.OnChange += OnEvacuatedChanged;
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

        /// <summary>Serveur : le joueur saute avec l'Arche (SPEC §3.2) — gelé, intouchable, objets conservés.</summary>
        [Server]
        public void Evacuate()
        {
            if (_dead.Value || _evacuated.Value)
                return;
            _evacuated.Value = true;
        }

        private void OnEvacuatedChanged(bool prev, bool next, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return;
            if (next)
                _motor.SetSeated(true, false); // gel « à bord » : l'écran récap prend le relais
        }

        private void OnDeadChanged(bool prev, bool next, bool asServer)
        {
            // Le host reçoit les deux passes : n'appliquer l'état qu'une fois.
            if (asServer && IsClientInitialized)
                return;
            if (!next)
                return;

            _motor.SetSeated(true, false); // gèle le corps (cadavre)

            foreach (Renderer r in _corpseRenderers)
                if (r != null)
                    r.material.color = Color.Lerp(r.material.color, Color.black, 0.75f);

            if (IsOwner)
            {
                if (_look != null)
                    _look.enabled = false;
                if (_spectator != null)
                    _spectator.Begin();
            }
        }
    }
}
