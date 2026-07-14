using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables navette 2 places (SPEC §4.3) — vol 6DOF assisté, atterrissage, coque.</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Shuttle", fileName = "SO_Shuttle_")]
    public class ShuttleDef : ScriptableObject
    {
        [Header("Vol (m/s, m/s²)")]
        [Tooltip("Vitesse max avant/arrière (SPEC : ~100 m/s).")]
        public float MaxForwardSpeed = 100f;
        public float MaxStrafeSpeed = 40f;
        public float MaxVerticalSpeed = 40f;
        public float LinearAcceleration = 30f;
        [Tooltip("Amortissement du flight assist quand aucun input (1/s).")]
        public float AssistDamping = 1.2f;

        [Header("Rotation (deg/s)")]
        public float PitchSpeed = 70f;
        public float YawSpeed = 70f;
        public float RollSpeed = 90f;
        [Tooltip("Réactivité de la rotation (1/s) — amortissement de la vitesse angulaire.")]
        public float AngularSharpness = 6f;

        [Header("Flight assist / auto-level")]
        [Tooltip("Altitude sol (m) sous laquelle l'auto-level s'active en champ de gravité.")]
        public float AutoLevelAltitude = 25f;
        [Tooltip("Vitesse de l'auto-level (1/s).")]
        public float AutoLevelSharpness = 2.5f;

        [Header("Atterrissage (auto-snap)")]
        [Tooltip("Vitesse verticale max (m/s) pour un posé propre.")]
        public float MaxLandingSpeed = 8f;
        [Tooltip("Inclinaison max (deg) vs la normale du sol pour un posé propre.")]
        public float MaxLandingTiltDegrees = 25f;
        [Tooltip("Distance sol (m) de déclenchement du snap.")]
        public float SnapDistance = 3f;
        [Tooltip("Garde au sol au posé (m) — hauteur des patins sous l'origine.")]
        public float LandingClearance = 0.2f;

        [Header("Coque")]
        public float MaxHull = 100f;
        [Tooltip("Vitesse d'impact (m/s) sans dégâts en dessous.")]
        public float ImpactSpeedThreshold = 10f;
        [Tooltip("Dégâts par m/s au-delà du seuil.")]
        public float ImpactDamagePerSpeed = 4f;
        [Tooltip("Impulsion d'éjection des occupants à la destruction (m/s).")]
        public float EjectionSpeed = 12f;
    }
}
