using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Règles pures d'atterrissage et de dégâts d'impact (SPEC §4.3). Testé en EditMode.</summary>
    public static class LandingEvaluator
    {
        /// <summary>Posé propre : vitesse d'approche et inclinaison sous les seuils du def.</summary>
        public static bool CanSnap(float approachSpeed, float tiltDegrees, ShuttleDef def)
            => approachSpeed <= def.MaxLandingSpeed && tiltDegrees <= def.MaxLandingTiltDegrees;

        /// <summary>Inclinaison (deg) entre l'up du vaisseau et la normale du sol.</summary>
        public static float TiltDegrees(Vector3 shipUp, Vector3 groundNormal)
            => Vector3.Angle(shipUp, groundNormal);

        /// <summary>Dégâts de coque pour une vitesse d'impact donnée (0 sous le seuil, linéaire au-delà).</summary>
        public static float ImpactDamage(float impactSpeed, ShuttleDef def)
            => Mathf.Max(0f, impactSpeed - def.ImpactSpeedThreshold) * def.ImpactDamagePerSpeed;
    }
}
