using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Règles pures de l'onde de choc (SPEC §4.10) : expansion linéaire, capture par rayon.</summary>
    public static class ShockwaveModel
    {
        /// <summary>Rayon (m) après <paramref name="elapsedSeconds"/> d'expansion.</summary>
        public static float RadiusAt(float elapsedSeconds, float speed, float startRadius)
            => startRadius + speed * Mathf.Max(0f, elapsedSeconds);

        /// <summary>Vrai si le point est englouti par l'onde (rayon inclus).</summary>
        public static bool Catches(Vector3 center, float radius, Vector3 point)
            => (point - center).sqrMagnitude <= radius * radius;
    }
}
