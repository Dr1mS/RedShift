using NUnit.Framework;
using Redshift.Gameplay;
using UnityEngine;

namespace Redshift.Tests.EditMode
{
    /// <summary>Perception des monstres (SPEC §4.8) : ouïe par vitesse, cônes de regard/lampe.</summary>
    public class CreaturePerceptionTests
    {
        // Ranges : silencieux 2.5, marche 10, sprint 20 ; seuils : 3 (marche), 6 (sprint).
        private static float Hearing(float speed)
            => CreaturePerception.HearingRadius(speed, 2.5f, 10f, 20f, 3f, 6f);

        [Test]
        public void Hearing_CrouchSpeed_IsQuiet()
            => Assert.That(Hearing(2.2f), Is.EqualTo(2.5f), "accroupi (2.2 m/s) : quasi silencieux — contre-jeu du Fouisseur");

        [Test]
        public void Hearing_WalkSpeed_IsAudible()
            => Assert.That(Hearing(4.5f), Is.EqualTo(10f));

        [Test]
        public void Hearing_SprintSpeed_IsLoud()
            => Assert.That(Hearing(7.5f), Is.EqualTo(20f));

        [Test]
        public void Hearing_Thresholds_AreInclusive()
        {
            Assert.That(Hearing(3f), Is.EqualTo(10f));
            Assert.That(Hearing(6f), Is.EqualTo(20f));
        }

        [Test]
        public void Hearing_Stationary_IsQuiet()
            => Assert.That(Hearing(0f), Is.EqualTo(2.5f));

        [Test]
        public void Cone_TargetInFront_IsSeen()
            => Assert.That(CreaturePerception.IsInCone(Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 5f), 45f, 20f), Is.True);

        [Test]
        public void Cone_TargetBehind_IsNotSeen()
            => Assert.That(CreaturePerception.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -5f), 45f, 20f), Is.False);

        [Test]
        public void Cone_TargetOutOfRange_IsNotSeen()
            => Assert.That(CreaturePerception.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 25f), 45f, 20f), Is.False);

        [Test]
        public void Cone_TargetOutsideHalfAngle_IsNotSeen()
            => Assert.That(CreaturePerception.IsInCone(Vector3.zero, Vector3.forward, new Vector3(5f, 0f, 2f), 45f, 20f), Is.False);

        [Test]
        public void Cone_SamePosition_IsNotSeen()
            => Assert.That(CreaturePerception.IsInCone(Vector3.zero, Vector3.forward, Vector3.zero, 45f, 20f), Is.False);
    }
}
