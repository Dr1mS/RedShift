using NUnit.Framework;
using Redshift.Gameplay;

namespace Redshift.Tests.EditMode
{
    /// <summary>Minage (SPEC §4.5) : chaleur/surchauffe du laser et rendement des filons.</summary>
    public class MiningModelTests
    {
        // --- Chaleur : 2 s pour surchauffer, 1 s pour refroidir, réarmement à 0.5.

        private static LaserHeatModel NewHeat() => new LaserHeatModel(2f, 1f, 0.5f);

        [Test]
        public void Heat_AccumulatesWhileFiring()
        {
            var heat = NewHeat();
            Assert.That(heat.Tick(true, 1f), Is.True);
            Assert.That(heat.Heat01, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(heat.CanFire, Is.True);
        }

        [Test]
        public void Heat_ReachingMax_Overheats()
        {
            var heat = NewHeat();
            heat.Tick(true, 1f);
            heat.Tick(true, 1f);
            Assert.That(heat.Overheated, Is.True);
            Assert.That(heat.Tick(true, 0.1f), Is.False, "surchauffé : pause forcée");
        }

        [Test]
        public void Heat_CoolsAndReArmsBelowThreshold()
        {
            var heat = NewHeat();
            heat.Tick(true, 2f); // surchauffe
            heat.Tick(true, 0.4f); // refroidit à ~0.6 : toujours bloqué
            Assert.That(heat.CanFire, Is.False);
            heat.Tick(true, 0.2f); // ~0.4 ≤ 0.5 : réarmé
            Assert.That(heat.CanFire, Is.True);
            Assert.That(heat.Tick(true, 0.1f), Is.True);
        }

        [Test]
        public void Heat_CoolsToZeroFloor()
        {
            var heat = NewHeat();
            heat.Tick(true, 1f);
            heat.Tick(false, 10f);
            Assert.That(heat.Heat01, Is.EqualTo(0f));
        }

        // --- Rendement : filon 100 HP → 5 unités.

        [Test]
        public void Yield_SpawnsProportionallyToDamage()
        {
            var vein = new VeinYieldTracker(100f, 5);
            Assert.That(vein.ApplyDamage(19f), Is.EqualTo(0), "0.95 unité due : rien encore");
            Assert.That(vein.ApplyDamage(1f), Is.EqualTo(1), "1.0 unité due");
            Assert.That(vein.ApplyDamage(60f), Is.EqualTo(3), "4 unités dues au total");
        }

        [Test]
        public void Yield_FullDamage_SpawnsExactlyAllUnits()
        {
            var vein = new VeinYieldTracker(100f, 5);
            Assert.That(vein.ApplyDamage(100f), Is.EqualTo(5));
            Assert.That(vein.Depleted, Is.True);
        }

        [Test]
        public void Yield_OverDamage_NeverExceedsTotal()
        {
            var vein = new VeinYieldTracker(100f, 5);
            vein.ApplyDamage(80f);
            Assert.That(vein.ApplyDamage(1000f), Is.EqualTo(1));
            Assert.That(vein.ApplyDamage(50f), Is.EqualTo(0));
            Assert.That(vein.UnitsSpawned, Is.EqualTo(5));
        }

        [Test]
        public void Yield_NegativeDamage_Ignored()
        {
            var vein = new VeinYieldTracker(100f, 5);
            Assert.That(vein.ApplyDamage(-30f), Is.EqualTo(0));
            Assert.That(vein.RemainingHp, Is.EqualTo(100f));
        }

        [Test]
        public void Yield_TracksRemainingHp()
        {
            var vein = new VeinYieldTracker(100f, 5);
            vein.ApplyDamage(35f);
            Assert.That(vein.RemainingHp, Is.EqualTo(65f).Within(1e-3f));
        }
    }
}
