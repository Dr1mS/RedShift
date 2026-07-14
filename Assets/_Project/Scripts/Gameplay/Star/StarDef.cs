using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables de l'étoile (SPEC §4.10, §7) : visuel piloté par l'instabilité + onde de choc.</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Star", fileName = "SO_Star_")]
    public class StarDef : ScriptableObject
    {
        [Header("Visuel étoile (la DA EST le timer, SPEC §7)")]
        public Color StarStableColor = new(1f, 0.85f, 0.55f);
        public Color StarCriticalColor = new(1f, 0.22f, 0.08f);
        [Tooltip("Gonflement du visuel à l'approche de l'éruption (multiplicateur d'échelle).")]
        public float CollapseScaleMultiplier = 2.5f;

        [Header("Lumière globale du système")]
        public Color LightStableColor = new(1f, 0.957f, 0.839f);
        public Color LightCriticalColor = new(1f, 0.45f, 0.32f);
        public float LightStableIntensity = 1f;
        public float LightCriticalIntensity = 1.4f;

        [Header("Onde de choc (seul collider létal, SPEC §4.10)")]
        [Tooltip("Vitesse d'expansion (m/s) — réglée pour rattraper une navette non boostée (~100 m/s).")]
        public float ShockwaveSpeed = 120f;
        [Tooltip("Rayon initial (m) — approx. le rayon du visuel de l'étoile.")]
        public float ShockwaveStartRadius = 250f;
        [Tooltip("Rayon (m) au-delà duquel l'onde despawn (couvre le système de 8 km).")]
        public float ShockwaveMaxRadius = 9000f;
    }
}
