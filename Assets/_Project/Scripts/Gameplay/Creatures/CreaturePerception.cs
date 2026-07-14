using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Règles de perception pures des monstres (SPEC §4.8) — testables EditMode.</summary>
    public static class CreaturePerception
    {
        /// <summary>
        /// Portée d'audition en fonction de la vitesse observée du joueur : accroupi/lent
        /// = silencieux, marche = audible, sprint = bruyant. C'est le contre-jeu du Fouisseur.
        /// </summary>
        public static float HearingRadius(float speed, float quietRange, float walkRange, float sprintRange, float walkThreshold, float sprintThreshold)
        {
            if (speed >= sprintThreshold)
                return sprintRange;
            return speed >= walkThreshold ? walkRange : quietRange;
        }

        /// <summary>Vrai si la cible est dans le cône (demi-angle) et la portée de l'observateur.</summary>
        public static bool IsInCone(Vector3 observerPosition, Vector3 observerForward, Vector3 targetPosition, float halfAngleDegrees, float maxRange)
        {
            Vector3 to = targetPosition - observerPosition;
            if (to.sqrMagnitude > maxRange * maxRange || to.sqrMagnitude < 1e-6f)
                return false;
            return Vector3.Angle(observerForward, to) <= halfAngleDegrees;
        }
    }
}
