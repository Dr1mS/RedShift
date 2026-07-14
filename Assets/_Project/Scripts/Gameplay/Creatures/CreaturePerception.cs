using System.Collections.Generic;
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

        /// <summary>
        /// Vrai si le joueur à <paramref name="playerIndex"/> est « isolé » : aucun AUTRE joueur
        /// vivant n'est à moins de <paramref name="isolationRadius"/> de lui (SPEC §4.8 : le Pâle
        /// traque les joueurs isolés). Le voisinage se calcule contre TOUS les joueurs vivants
        /// fournis, pas seulement ceux visibles par la créature — un coéquipier hors ligne de vue
        /// du Pâle « protège » quand même son voisin. Un joueur seul est isolé par définition
        /// (le Pâle reste dangereux en solo).
        /// </summary>
        public static bool IsIsolated(IReadOnlyList<Vector3> alivePlayerPositions, int playerIndex, float isolationRadius)
        {
            if (alivePlayerPositions == null || playerIndex < 0 || playerIndex >= alivePlayerPositions.Count)
                return false;
            float sqr = isolationRadius * isolationRadius;
            Vector3 self = alivePlayerPositions[playerIndex];
            for (int i = 0; i < alivePlayerPositions.Count; i++)
            {
                if (i == playerIndex)
                    continue;
                if ((alivePlayerPositions[i] - self).sqrMagnitude < sqr)
                    return false; // un voisin vivant assez proche → non isolé
            }
            return true;
        }

        /// <summary>
        /// Choisit l'index (dans <paramref name="alivePlayerPositions"/>) de la cible du Pâle
        /// parmi les <paramref name="candidateIndices"/> (joueurs vivants + en ligne de vue + à
        /// portée, filtrés en amont). Préfère un candidat isolé (le plus proche de
        /// <paramref name="observerPosition"/> parmi les isolés) ; à défaut d'isolé, repli sur le
        /// candidat le plus proche (comportement v1 P3, le Pâle reste dangereux à plusieurs
        /// groupés). L'isolement est calculé contre TOUS les vivants (voisinage complet).
        /// Retourne -1 si aucun candidat.
        /// </summary>
        public static int SelectStalkTarget(
            IReadOnlyList<Vector3> alivePlayerPositions,
            IReadOnlyList<int> candidateIndices,
            Vector3 observerPosition,
            float isolationRadius)
        {
            if (alivePlayerPositions == null || candidateIndices == null || candidateIndices.Count == 0)
                return -1;

            int bestIsolated = -1;
            float bestIsolatedSqr = float.MaxValue;
            int bestAny = -1;
            float bestAnySqr = float.MaxValue;

            foreach (int idx in candidateIndices)
            {
                if (idx < 0 || idx >= alivePlayerPositions.Count)
                    continue;
                float sqr = (alivePlayerPositions[idx] - observerPosition).sqrMagnitude;
                if (sqr < bestAnySqr)
                {
                    bestAnySqr = sqr;
                    bestAny = idx;
                }
                if (IsIsolated(alivePlayerPositions, idx, isolationRadius) && sqr < bestIsolatedSqr)
                {
                    bestIsolatedSqr = sqr;
                    bestIsolated = idx;
                }
            }

            return bestIsolated != -1 ? bestIsolated : bestAny;
        }
    }
}
