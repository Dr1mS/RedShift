using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Règles de perception pures des monstres (SPEC §4.8) — testables EditMode.</summary>
    public static class CreaturePerception
    {
        /// <summary>
        /// Au-delà de ce déplacement répliqué en une frame, on considère un téléport (portail
        /// de ruine ~2000 m, respawn, éjection de coque), pas un mouvement continu : la vitesse
        /// observée ne doit alors PAS être mise à jour, sinon le Fouisseur « entendrait » le
        /// joueur au tick de sa traversée (piège connu P3, vrai pour joueurs owner ET distants).
        /// Protection technique réseau, cohérente avec le seuil de téléport du NetworkTransform
        /// (100 m) : bien au-dessus de tout mouvement légitime par frame (navette 20 m/s @30 fps
        /// ≈ 0.67 m/frame), bien en-dessous d'une traversée de poche (~2000 m).
        /// </summary>
        public const float TeleportGuardDistance = 50f;

        /// <summary>
        /// Vrai si un déplacement observé sur une frame doit compter comme bruit continu
        /// (à intégrer dans la vitesse observée). Faux pour un téléport (delta ≥ garde).
        /// </summary>
        public static bool IsContinuousMotion(float frameDistance)
            => frameDistance < TeleportGuardDistance;

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
