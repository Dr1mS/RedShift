using NUnit.Framework;
using Redshift.Gameplay;
using UnityEngine;

namespace Redshift.Tests.EditMode
{
    /// <summary>Règles d'atterrissage auto-snap et de dégâts d'impact (SPEC §4.3).</summary>
    public class LandingEvaluatorTests
    {
        private ShuttleDef def;

        [SetUp]
        public void SetUp()
        {
            def = ScriptableObject.CreateInstance<ShuttleDef>();
            def.MaxLandingSpeed = 8f;
            def.MaxLandingTiltDegrees = 25f;
            def.ImpactSpeedThreshold = 10f;
            def.ImpactDamagePerSpeed = 4f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(def);

        [Test]
        public void CanSnap_SlowAndFlat_IsTrue()
            => Assert.That(LandingEvaluator.CanSnap(5f, 10f, def), Is.True);

        [Test]
        public void CanSnap_TooFast_IsFalse()
            => Assert.That(LandingEvaluator.CanSnap(9f, 10f, def), Is.False);

        [Test]
        public void CanSnap_TooTilted_IsFalse()
            => Assert.That(LandingEvaluator.CanSnap(5f, 30f, def), Is.False);

        [Test]
        public void CanSnap_AtExactThresholds_IsTrue()
            => Assert.That(LandingEvaluator.CanSnap(8f, 25f, def), Is.True);

        [Test]
        public void TiltDegrees_UpAlignedWithNormal_IsZero()
            => Assert.That(LandingEvaluator.TiltDegrees(Vector3.up, Vector3.up), Is.EqualTo(0f).Within(1e-3f));

        [Test]
        public void TiltDegrees_45Degrees()
        {
            Vector3 tilted = new Vector3(1f, 1f, 0f).normalized;
            Assert.That(LandingEvaluator.TiltDegrees(tilted, Vector3.up), Is.EqualTo(45f).Within(1e-2f));
        }

        [Test]
        public void ImpactDamage_BelowThreshold_IsZero()
            => Assert.That(LandingEvaluator.ImpactDamage(9.9f, def), Is.EqualTo(0f));

        [Test]
        public void ImpactDamage_AboveThreshold_IsLinear()
            => Assert.That(LandingEvaluator.ImpactDamage(25f, def), Is.EqualTo(60f).Within(1e-3f));

        [Test]
        public void ImpactDamage_LethalCrash_ExceedsMaxHull()
        {
            def.MaxHull = 100f;
            // 40 m/s au-dessus du seuil : de quoi détruire la coque d'un coup.
            Assert.That(LandingEvaluator.ImpactDamage(50f, def), Is.GreaterThan(def.MaxHull));
        }
    }
}
