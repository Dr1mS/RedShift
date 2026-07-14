using System.Collections.Generic;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Champ de gravité sphérique statique (SPEC §4.2). Les planètes sont immobiles (D4) :
    /// le centre est lu à la construction du cache. Pas de NetworkBehaviour — donnée de
    /// scène identique chez tous les pairs, aucune synchro nécessaire.
    /// </summary>
    [DisallowMultipleComponent]
    public class GravitySource : MonoBehaviour
    {
        private static readonly List<GravitySource> sources = new();
        private static readonly List<GravityFieldData> fieldCache = new();
        private static bool cacheDirty = true;
        private static int nextId = 1;

        [SerializeField, Tooltip("Rayon de la surface marchable (m).")]
        private float _surfaceRadius = 200f;

        [SerializeField, Tooltip("Rayon d'influence du champ (m). Au-delà : micro-gravité.")]
        private float _influenceRadius = 400f;

        [SerializeField, Tooltip("Accélération à la surface (m/s²), constante dans tout le champ.")]
        private float _surfaceGravity = 9.81f;

        [SerializeField, Tooltip("Priorité de champ : le plus haut gagne (lune > planète, poche > tout).")]
        private int _priority = 0;

        [SerializeField, Tooltip("Champ uniforme (poches d'intérieur, D6) : gravité constante selon le -up de l'objet.")]
        private bool _uniform;

        private int id;

        public int Id => id;
        public float SurfaceRadius => _surfaceRadius;
        public float InfluenceRadius => _influenceRadius;
        public float SurfaceGravity => _surfaceGravity;
        public int Priority => _priority;

        /// <summary>Snapshot de tous les champs actifs, régénéré quand une source (dés)apparaît.</summary>
        public static IReadOnlyList<GravityFieldData> Fields
        {
            get
            {
                if (cacheDirty)
                {
                    fieldCache.Clear();
                    for (int i = 0; i < sources.Count; i++)
                    {
                        GravitySource s = sources[i];
                        fieldCache.Add(new GravityFieldData(s.id, s.transform.position, s._surfaceRadius, s._influenceRadius, s._surfaceGravity, s._priority, s._uniform, -s.transform.up));
                    }
                    cacheDirty = false;
                }
                return fieldCache;
            }
        }

        /// <summary>À appeler si une source a bougé après son activation (outillage/tests uniquement — D4 : planètes statiques).</summary>
        public static void InvalidateCache() => cacheDirty = true;

        /// <summary>Configuration par code (outillage de scène et tests). En scène, passer par l'inspecteur.</summary>
        public void Configure(float surfaceRadius, float influenceRadius, float surfaceGravity, int priority, bool uniform = false)
        {
            _surfaceRadius = surfaceRadius;
            _influenceRadius = influenceRadius;
            _surfaceGravity = surfaceGravity;
            _priority = priority;
            _uniform = uniform;
            cacheDirty = true;
        }

        public static GravitySource FindById(int id)
        {
            for (int i = 0; i < sources.Count; i++)
                if (sources[i].id == id)
                    return sources[i];
            return null;
        }

        private void OnEnable()
        {
            if (id == 0)
                id = nextId++;
            sources.Add(this);
            cacheDirty = true;
        }

        private void OnDisable()
        {
            sources.Remove(this);
            cacheDirty = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _surfaceRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _influenceRadius);
        }
    }
}
