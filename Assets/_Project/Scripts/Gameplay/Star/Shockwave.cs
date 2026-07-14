using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Onde de choc de la supernova (SPEC §4.10) : sphère en expansion, seul collider létal.
    /// Le rayon est déterministe des deux côtés (tick réseau de départ + vitesse), pas de
    /// physique : le serveur balaie les victimes par distance (fiable, pas de tunneling).
    /// </summary>
    public class Shockwave : NetworkBehaviour
    {
        [SerializeField] private StarDef _def;
        [SerializeField, Tooltip("Sphère visuelle (échelle = diamètre).")]
        private Transform _visual;

        private readonly SyncVar<uint> _startTick = new();

        // Serveur uniquement : victimes candidates, figées au spawn de l'onde.
        private PlayerHealth[] players;
        private ShuttleHull[] shuttles;

        public float Radius => ShockwaveModel.RadiusAt(ElapsedSeconds, _def.ShockwaveSpeed, _def.ShockwaveStartRadius);

        private float ElapsedSeconds
            => _startTick.Value == 0 ? 0f : (float)((TimeManager.Tick - _startTick.Value) * TimeManager.TickDelta);

        public override void OnStartServer()
        {
            base.OnStartServer();
            _startTick.Value = TimeManager.Tick;
            players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
            shuttles = FindObjectsByType<ShuttleHull>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            float radius = Radius;
            if (_visual != null)
                _visual.localScale = Vector3.one * (radius * 2f);

            if (!IsServerInitialized)
                return;

            Vector3 center = transform.position;
            foreach (PlayerHealth player in players)
                if (player != null && !player.IsDead && ShockwaveModel.Catches(center, radius, player.transform.position))
                    player.Kill();

            foreach (ShuttleHull hull in shuttles)
                if (hull != null && !hull.IsDestroyed && ShockwaveModel.Catches(center, radius, hull.transform.position))
                    hull.ApplyDamage(float.MaxValue);

            if (radius >= _def.ShockwaveMaxRadius)
                Despawn();
        }
    }
}
