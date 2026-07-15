using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Visuel COSMÉTIQUE de la Nuée (SPEC §4.8) : un nuage de boids simulé LOCALEMENT chez chaque
    /// client (et le host) autour du centre répliqué de <see cref="SwarmCreature"/>. Aucune
    /// réplication par boid : chaque pair fait tourner son propre nuage aléatoire ; seuls le centre
    /// (NetworkTransform) et l'état d'agitation (SyncVar) sont partagés. Les dégâts se décident
    /// serveur depuis la distance centre↔joueur, jamais depuis ces positions — donc aucun besoin
    /// de seed déterministe. Greybox : petits cubes instanciés en enfants.
    /// </summary>
    public class SwarmBoidVisual : MonoBehaviour
    {
        [SerializeField] private SwarmCreature _swarm;
        [SerializeField, Tooltip("Racine des boids (créée à la volée si absente).")]
        private Transform _boidRoot;
        [SerializeField, Tooltip("Taille d'un boid (échelle locale du cube).")]
        private float _boidSize = 0.35f;
        [SerializeField, Tooltip("Matériau des boids (greybox). Optionnel : couleur par défaut sinon.")]
        private Material _boidMaterial;
        [SerializeField, Tooltip("Netteté du lissage du spread quand l'agitation change (1/s).")]
        private float _spreadLerpSharpness = 4f;

        private NueeDef def;
        private Transform[] boids;
        private Vector3[] anchors;   // point d'ancrage propre à chaque boid dans le nuage
        private float[] phases;      // déphasage d'agitation par boid
        private float currentSpread;

        private void Start()
        {
            if (_swarm == null)
                _swarm = GetComponentInParent<SwarmCreature>();
            def = _swarm != null ? _swarm.Def : null;
            if (def == null || def.BoidCount <= 0)
            {
                enabled = false;
                return;
            }

            if (_boidRoot == null)
            {
                var rootGo = new GameObject("Boids");
                _boidRoot = rootGo.transform;
                _boidRoot.SetParent(transform, false);
            }

            currentSpread = def.SpreadIdle;
            boids = new Transform[def.BoidCount];
            anchors = new Vector3[def.BoidCount];
            phases = new float[def.BoidCount];

            for (int i = 0; i < def.BoidCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Boid";
                // Pas de collision cosmétique : détruire le collider (ne jamais bloquer le raycast de minage).
                var col = go.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);
                if (_boidMaterial != null)
                    go.GetComponent<MeshRenderer>().sharedMaterial = _boidMaterial;
                Transform t = go.transform;
                t.SetParent(_boidRoot, false);
                t.localScale = Vector3.one * _boidSize;

                boids[i] = t;
                anchors[i] = Random.onUnitSphere * Mathf.Lerp(0.3f, 1f, Random.value);
                phases[i] = Random.value * 100f;
            }
        }

        private void Update()
        {
            if (boids == null)
                return;

            byte state = _swarm != null ? _swarm.Agitation : SwarmCreature.StateIdle;
            float targetSpread = state == SwarmCreature.StateIdle ? def.SpreadIdle : def.SpreadAgitated;
            float k = 1f - Mathf.Exp(-_spreadLerpSharpness * Time.deltaTime);
            currentSpread = Mathf.Lerp(currentSpread, targetSpread, k);

            // Plus l'essaim est agité, plus l'agitation locale est vive (nuage nerveux).
            float jitterAmp = state == SwarmCreature.StateIdle ? def.BoidJitterSpeed * 0.4f : def.BoidJitterSpeed;
            float time = Time.time;
            Vector3 center = transform.position;

            for (int i = 0; i < boids.Length; i++)
            {
                // Ancre dans la sphère du nuage + bruit sinusoïdal déphasé (pas de physique).
                Vector3 baseOffset = anchors[i] * currentSpread;
                float ph = phases[i];
                Vector3 jitter = new Vector3(
                    Mathf.Sin(time * 1.7f + ph),
                    Mathf.Sin(time * 2.3f + ph * 1.3f),
                    Mathf.Sin(time * 1.9f + ph * 0.7f)) * (jitterAmp * 0.15f);
                boids[i].position = center + baseOffset + jitter;
            }
        }
    }
}
