using System.Collections.Generic;
using NUnit.Framework;
using Redshift.Gameplay;
using UnityEngine;

namespace Redshift.Tests.EditMode
{
    /// <summary>Champ de gravité uniforme des poches d'intérieur (SPEC D6) : y-up standard.</summary>
    public class UniformGravityTests
    {
        private static GravityFieldData Pocket(int id = 1)
            => new(id, new Vector3(2000f, -1000f, 0f), 60f, 120f, 9.81f, 10, true, Vector3.down);

        [Test]
        public void AccelerationAt_Uniform_IsConstantDirectionEverywhere()
        {
            var pocket = Pocket();
            var atCenter = pocket.AccelerationAt(pocket.Center);
            var offCenter = pocket.AccelerationAt(pocket.Center + new Vector3(40f, 10f, -25f));
            Assert.That(atCenter, Is.EqualTo(Vector3.down * 9.81f));
            Assert.That(offCenter, Is.EqualTo(atCenter), "uniforme : indépendant du point");
        }

        [Test]
        public void AccelerationAt_Radial_StillPointsToCenter()
        {
            var planet = new GravityFieldData(2, Vector3.zero, 200f, 400f, 9.81f, 0);
            var acc = planet.AccelerationAt(new Vector3(0f, 210f, 0f));
            Assert.That(acc.normalized.y, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void Contains_Uniform_UsesSphericalInfluence()
        {
            var pocket = Pocket();
            Assert.That(pocket.Contains(pocket.Center + Vector3.right * 119f), Is.True);
            Assert.That(pocket.Contains(pocket.Center + Vector3.right * 121f), Is.False);
        }

        [Test]
        public void Resolver_PicksPocketInsideItsInfluence()
        {
            var fields = new List<GravityFieldData>
            {
                new(2, Vector3.zero, 200f, 400f, 9.81f, 0), // planète à l'origine
                Pocket(),
            };
            int resolved = GravityResolver.Resolve(fields, new Vector3(2010f, -995f, 3f), GravityResolver.NoField, 0.03f);
            Assert.That(resolved, Is.EqualTo(1));
        }

        [Test]
        public void Resolver_PocketIsMicroGravityFreeOutside()
        {
            var fields = new List<GravityFieldData> { Pocket() };
            int resolved = GravityResolver.Resolve(fields, new Vector3(2000f, -700f, 0f), 1, 0.03f);
            Assert.That(resolved, Is.EqualTo(GravityResolver.NoField), "hors influence : micro-gravité");
        }
    }
}
