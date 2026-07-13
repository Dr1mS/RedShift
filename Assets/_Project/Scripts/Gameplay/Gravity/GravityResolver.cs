using System.Collections.Generic;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Sélection du champ de gravité dominant en un point (SPEC §4.2) :
    /// priorité d'abord, puis proximité de surface. L'hystérésis évite toute
    /// oscillation à la frontière entre deux champs. Logique pure, testée en EditMode.
    /// Indépendante de l'ordre d'itération des champs.
    /// </summary>
    public static class GravityResolver
    {
        public const int NoField = -1;

        /// <summary>
        /// Retourne l'id du champ dominant, ou <see cref="NoField"/> en micro-gravité.
        /// Hystérésis double :
        /// 1. le champ courant reste éligible jusqu'à influenceRadius * (1 + hysteresis) ;
        /// 2. à priorité égale, un challenger ne prend la main que s'il est plus proche
        ///    de sa surface d'une marge absolue (hysteresis * influenceRadius du courant).
        /// Un champ de priorité strictement supérieure prend toujours la main.
        /// </summary>
        public static int Resolve(IReadOnlyList<GravityFieldData> fields, Vector3 point, int currentId, float hysteresis)
        {
            int maxPriority = int.MinValue;
            bool currentEligible = false;
            GravityFieldData current = default;

            for (int i = 0; i < fields.Count; i++)
            {
                GravityFieldData f = fields[i];
                bool isCurrent = f.Id == currentId;
                if (!f.Contains(point, isCurrent ? 1f + hysteresis : 1f))
                    continue;
                if (isCurrent)
                {
                    currentEligible = true;
                    current = f;
                }
                if (f.Priority > maxPriority)
                    maxPriority = f.Priority;
            }

            if (maxPriority == int.MinValue)
                return NoField;

            // Meilleur challenger (hors champ courant) à la priorité maximale.
            int bestId = NoField;
            float bestSurfaceDistance = float.MaxValue;
            for (int i = 0; i < fields.Count; i++)
            {
                GravityFieldData f = fields[i];
                if (f.Id == currentId || f.Priority != maxPriority || !f.Contains(point))
                    continue;
                float surfaceDistance = Vector3.Distance(point, f.Center) - f.SurfaceRadius;
                if (surfaceDistance < bestSurfaceDistance)
                {
                    bestSurfaceDistance = surfaceDistance;
                    bestId = f.Id;
                }
            }

            if (!currentEligible || current.Priority < maxPriority)
                return bestId; // Perte du champ courant ou priorité supérieure ailleurs.

            if (bestId == NoField)
                return currentId;

            // Priorité égale : le challenger doit gagner d'une marge franche.
            float currentSurfaceDistance = Vector3.Distance(point, current.Center) - current.SurfaceRadius;
            float margin = hysteresis * current.InfluenceRadius;
            return bestSurfaceDistance < currentSurfaceDistance - margin ? bestId : currentId;
        }
    }
}
