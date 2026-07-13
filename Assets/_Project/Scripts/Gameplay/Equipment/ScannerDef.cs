using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables du scanner v1 (SPEC §4.1 : ping des items/gisements/ruines dans un rayon).</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Scanner", fileName = "SO_Equip_Scanner")]
    public class ScannerDef : ScriptableObject
    {
        public float Radius = 30f;
        public float Cooldown = 3f;
        [Tooltip("Durée d'affichage des marqueurs (s).")]
        public float MarkerDuration = 4f;
        [Tooltip("Nombre max de cibles par ping.")]
        public int MaxTargets = 32;
    }
}
