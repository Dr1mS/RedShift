using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Règles pures du saut de fin de système (SPEC §3.2) — testables EditMode.</summary>
    public static class JumpRules
    {
        /// <summary>Vrai si le joueur est « à bord » (dans le rayon d'embarquement du cockpit).</summary>
        public static bool IsAboard(Vector3 playerPosition, Vector3 cockpitPosition, float boardingRadius)
            => (playerPosition - cockpitPosition).sqrMagnitude <= boardingRadius * boardingRadius;
    }
}
