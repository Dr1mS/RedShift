using NUnit.Framework;
using Redshift.Gameplay;
using UnityEngine;

namespace Redshift.Tests.EditMode
{
    /// <summary>Onde de choc (SPEC §4.10) : expansion linéaire et capture par rayon.</summary>
    public class ShockwaveModelTests
    {
        [Test]
        public void RadiusAt_Zero_IsStartRadius()
            => Assert.That(ShockwaveModel.RadiusAt(0f, 120f, 250f), Is.EqualTo(250f));

        [Test]
        public void RadiusAt_GrowsLinearly()
            => Assert.That(ShockwaveModel.RadiusAt(10f, 120f, 250f), Is.EqualTo(1450f));

        [Test]
        public void RadiusAt_NegativeElapsed_ClampsToStart()
            => Assert.That(ShockwaveModel.RadiusAt(-5f, 120f, 250f), Is.EqualTo(250f));

        [Test]
        public void Catches_PointInside_IsTrue()
            => Assert.That(ShockwaveModel.Catches(Vector3.zero, 100f, new Vector3(60f, 0f, 0f)), Is.True);

        [Test]
        public void Catches_PointOutside_IsFalse()
            => Assert.That(ShockwaveModel.Catches(Vector3.zero, 100f, new Vector3(0f, 101f, 0f)), Is.False);

        [Test]
        public void Catches_PointOnBoundary_IsTrue()
            => Assert.That(ShockwaveModel.Catches(Vector3.zero, 100f, new Vector3(100f, 0f, 0f)), Is.True);

        [Test]
        public void Shuttle_CannotOutrunWave()
        {
            // La SPEC exige que l'onde rattrape une navette non boostée (100 m/s < vitesse d'onde).
            var def = ScriptableObject.CreateInstance<StarDef>();
            var shuttle = ScriptableObject.CreateInstance<ShuttleDef>();
            Assert.That(def.ShockwaveSpeed, Is.GreaterThan(shuttle.MaxForwardSpeed));
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(shuttle);
        }
    }
}
