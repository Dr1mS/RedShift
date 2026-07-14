using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Filon minable (SPEC §4.5) : HP host-authoritative, les unités de minerai
    /// (WorldItem, autorité host) sortent proportionnellement aux dégâts.
    /// Le filon rétrécit visuellement puis disparaît une fois épuisé.
    /// </summary>
    public class OreVein : NetworkBehaviour
    {
        [SerializeField] private VeinDef _def;
        [SerializeField, Tooltip("Prefab d'unité de minerai (WorldItem réseau).")]
        private NetworkObject _orePrefab;
        [SerializeField, Tooltip("Point d'apparition des minerais (au-dessus du filon).")]
        private Transform _oreSpawnPoint;
        [SerializeField, Tooltip("Visuel rétréci avec les HP (racine si null).")]
        private Transform _visual;

        private readonly SyncVar<float> _hp = new();

        private VeinYieldTracker yield; // serveur uniquement
        private Vector3 initialVisualScale;

        public VeinDef Def => _def;
        public bool Depleted => _hp.Value <= 0f;

        private void Awake()
        {
            if (_visual == null)
                _visual = transform;
            initialVisualScale = _visual.localScale;
            _hp.OnChange += OnHpChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            yield = new VeinYieldTracker(_def.MaxHp, _def.OreUnits);
            _hp.Value = _def.MaxHp;
        }

        /// <summary>Serveur : dégâts de minage → HP, minerais, despawn à l'épuisement.</summary>
        [Server]
        public void ApplyMiningDamage(float damage, Vector3 hitPoint)
        {
            if (Depleted)
                return;

            int toSpawn = yield.ApplyDamage(damage);
            _hp.Value = yield.RemainingHp;

            for (int i = 0; i < toSpawn; i++)
                SpawnOre();

            if (yield.Depleted)
                Despawn();
        }

        [Server]
        private void SpawnOre()
        {
            Vector3 position = _oreSpawnPoint != null ? _oreSpawnPoint.position : transform.position + transform.up;
            NetworkObject ore = Instantiate(_orePrefab, position, Random.rotation);
            ServerManager.Spawn(ore);

            // Petite éjection aléatoire vers le haut local : réveil + autorité host (WorldItem).
            var item = ore.GetComponent<WorldItem>();
            Vector3 toss = (transform.up + Random.insideUnitSphere * 0.4f).normalized * _def.OreEjectSpeed;
            if (item != null)
                item.Release(position, ore.transform.rotation, toss);
        }

        private void OnHpChanged(float prev, float next, bool asServer)
        {
            float ratio = _def.MaxHp > 0f ? Mathf.Clamp01(next / _def.MaxHp) : 0f;
            _visual.localScale = initialVisualScale * Mathf.Lerp(0.35f, 1f, ratio);
        }
    }
}
