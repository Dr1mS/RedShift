using System.Collections.Generic;
using NUnit.Framework;
using Redshift.Core;

namespace Redshift.Tests.EditMode
{
    /// <summary>Timeline d'un système (SPEC §3.2) : transitions, timer, fenêtre de saut, éruption.</summary>
    public class SystemTimelineTests
    {
        private SystemTimeline timeline;

        [SetUp]
        public void SetUp() => timeline = new SystemTimeline(10f, 100f, 50f, 20f);

        [Test]
        public void InitialState_IsFtl_NoSupernovaPending()
        {
            Assert.That(timeline.CurrentPhase, Is.EqualTo(GamePhase.Ftl));
            Assert.That(timeline.TimeUntilSupernova, Is.EqualTo(float.PositiveInfinity));
            Assert.That(timeline.SupernovaErupted, Is.False);
        }

        [Test]
        public void StartSystem_EntersArrival()
        {
            timeline.StartSystem();
            Assert.That(timeline.CurrentPhase, Is.EqualTo(GamePhase.Arrival));
            Assert.That(timeline.PhaseRemaining, Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void Tick_BeforePhaseEnd_StaysInPhase()
        {
            timeline.StartSystem();
            timeline.Tick(9.9f);
            Assert.That(timeline.CurrentPhase, Is.EqualTo(GamePhase.Arrival));
        }

        [Test]
        public void Tick_PastPhaseEnd_CarriesOverflowIntoNextPhase()
        {
            timeline.StartSystem();
            timeline.Tick(12f);
            Assert.That(timeline.CurrentPhase, Is.EqualTo(GamePhase.Exploration));
            Assert.That(timeline.PhaseElapsed, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void Tick_HugeStep_CrossesAllPhasesAndErupts()
        {
            timeline.StartSystem();
            timeline.Tick(10f + 100f + 50f + 20f + 5f);
            Assert.That(timeline.CurrentPhase, Is.EqualTo(GamePhase.Jump));
            Assert.That(timeline.SupernovaErupted, Is.True, "le Collapse est allé à T-0 : éruption");
        }

        [Test]
        public void PhaseChanged_RaisedInOrder()
        {
            var seen = new List<GamePhase>();
            timeline.PhaseChanged += seen.Add;
            timeline.StartSystem();
            timeline.Tick(200f); // au-delà de tout
            Assert.That(seen, Is.EqualTo(new[]
            {
                GamePhase.Arrival, GamePhase.Exploration, GamePhase.Critical, GamePhase.Collapse, GamePhase.Jump,
            }));
        }

        [Test]
        public void TryJumpNow_OutsideCollapse_Fails()
        {
            timeline.StartSystem();
            timeline.Tick(15f); // Exploration
            Assert.That(timeline.TryJumpNow(), Is.False);
            Assert.That(timeline.CurrentPhase, Is.EqualTo(GamePhase.Exploration));
        }

        [Test]
        public void TryJumpNow_DuringCollapse_JumpsWithoutEruption()
        {
            timeline.StartSystem();
            timeline.Tick(10f + 100f + 50f + 1f); // Collapse entamé
            Assert.That(timeline.CurrentPhase, Is.EqualTo(GamePhase.Collapse));
            Assert.That(timeline.TryJumpNow(), Is.True);
            Assert.That(timeline.CurrentPhase, Is.EqualTo(GamePhase.Jump));
            Assert.That(timeline.SupernovaErupted, Is.False, "saut anticipé : pas d'éruption à ce stade");
        }

        [Test]
        public void TimeUntilSupernova_CountsDownAcrossPhases()
        {
            timeline.StartSystem();
            timeline.Tick(10f); // début Exploration
            Assert.That(timeline.TimeUntilSupernova, Is.EqualTo(170f).Within(1e-3f));
            timeline.Tick(30f);
            Assert.That(timeline.TimeUntilSupernova, Is.EqualTo(140f).Within(1e-3f));
        }

        [Test]
        public void Instability01_ZeroDuringArrival_OneAtJump()
        {
            timeline.StartSystem();
            timeline.Tick(5f);
            Assert.That(timeline.Instability01, Is.EqualTo(0f));
            timeline.Tick(500f);
            Assert.That(timeline.Instability01, Is.EqualTo(1f));
        }

        [Test]
        public void Instability01_GrowsMonotonically()
        {
            timeline.StartSystem();
            timeline.Tick(10f); // début Exploration : 0
            float previous = timeline.Instability01;
            Assert.That(previous, Is.EqualTo(0f).Within(1e-4f));
            for (int i = 0; i < 17; i++)
            {
                timeline.Tick(10f);
                Assert.That(timeline.Instability01, Is.GreaterThanOrEqualTo(previous));
                previous = timeline.Instability01;
            }
        }

        [Test]
        public void Tick_NonPositiveDelta_IsIgnored()
        {
            timeline.StartSystem();
            timeline.Tick(-5f);
            timeline.Tick(0f);
            Assert.That(timeline.CurrentPhase, Is.EqualTo(GamePhase.Arrival));
            Assert.That(timeline.PhaseElapsed, Is.EqualTo(0f));
        }
    }
}
