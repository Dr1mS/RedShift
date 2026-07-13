using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Instantané immuable d'un champ de gravité sphérique, découplé de la scène
    /// pour que <see cref="GravityResolver"/> reste de la logique pure testable en EditMode.
    /// </summary>
    public readonly struct GravityFieldData
    {
        public readonly int Id;
        public readonly Vector3 Center;
        public readonly float SurfaceRadius;
        public readonly float InfluenceRadius;
        public readonly float SurfaceGravity;
        public readonly int Priority;

        public GravityFieldData(int id, Vector3 center, float surfaceRadius, float influenceRadius, float surfaceGravity, int priority)
        {
            Id = id;
            Center = center;
            SurfaceRadius = surfaceRadius;
            InfluenceRadius = influenceRadius;
            SurfaceGravity = surfaceGravity;
            Priority = priority;
        }

        public bool Contains(Vector3 point, float radiusScale = 1f)
        {
            float r = InfluenceRadius * radiusScale;
            return (point - Center).sqrMagnitude <= r * r;
        }

        /// <summary>Accélération de gravité au point donné (constante dans le champ, direction vers le centre).</summary>
        public Vector3 AccelerationAt(Vector3 point)
        {
            Vector3 toCenter = Center - point;
            float sqr = toCenter.sqrMagnitude;
            if (sqr < 1e-6f)
                return Vector3.zero;
            return toCenter / Mathf.Sqrt(sqr) * SurfaceGravity;
        }
    }
}
