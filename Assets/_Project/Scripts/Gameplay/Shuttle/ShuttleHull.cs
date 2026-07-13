using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Coque de la navette : HP host-authoritative, destruction = épave non pilotable
    /// + éjection des occupants (SPEC §4.3). L'épave reste lootable (soute en P3).
    /// </summary>
    public class ShuttleHull : NetworkBehaviour
    {
        [SerializeField] private ShuttleDef _def;
        [SerializeField, Tooltip("Renderers assombris à la destruction (épave).")]
        private Renderer[] _wreckRenderers;

        private readonly SyncVar<float> _hull = new();
        private readonly SyncVar<bool> _destroyed = new();

        public float HullNormalized => _def == null ? 1f : _hull.Value / _def.MaxHull;
        public bool IsDestroyed => _destroyed.Value;

        private void Awake()
        {
            _destroyed.OnChange += OnDestroyedChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _hull.Value = _def.MaxHull;
        }

        /// <summary>Dégâts détectés côté contrôleur (collisions owner-auth) et validés serveur.</summary>
        public void RequestDamage(float damage)
        {
            if (IsServerInitialized)
                ApplyDamage(damage);
            else
                DamageServerRpc(damage);
        }

        [ServerRpc(RequireOwnership = true)]
        private void DamageServerRpc(float damage) => ApplyDamage(damage);

        [Server]
        public void ApplyDamage(float damage)
        {
            if (_destroyed.Value)
                return;
            _hull.Value = Mathf.Max(0f, _hull.Value - damage);
            if (_hull.Value <= 0f)
                DestroyHull();
        }

        [Server]
        private void DestroyHull()
        {
            _destroyed.Value = true;

            // Éjection des occupants avant de retirer le contrôle (clip potentiel assumé — SPEC).
            foreach (Seat seat in GetComponentsInChildren<Seat>())
                seat.ServerEject(_def.EjectionSpeed);

            // L'épave revient au host : plus de pilote.
            NetworkObject.RemoveOwnership();
        }

        private void OnDestroyedChanged(bool prev, bool next, bool asServer)
        {
            if (!next)
                return;
            var controller = GetComponent<ShuttleController>();
            if (controller != null)
                controller.enabled = false;
            foreach (Renderer r in _wreckRenderers)
                if (r != null)
                    r.material.color = Color.Lerp(r.material.color, Color.black, 0.7f);
        }
    }
}
