using NUnit.Framework;
using Redshift.Gameplay;
using UnityEngine;

namespace Redshift.Tests.EditMode
{
    /// <summary>Règles du saut de fin de système (SPEC §3.2) : rayon d'embarquement.</summary>
    public class JumpRulesTests
    {
        [Test]
        public void IsAboard_InsideRadius_IsTrue()
            => Assert.That(JumpRules.IsAboard(new Vector3(5f, 0f, 3f), Vector3.zero, 12f), Is.True);

        [Test]
        public void IsAboard_OutsideRadius_IsFalse()
            => Assert.That(JumpRules.IsAboard(new Vector3(15f, 0f, 0f), Vector3.zero, 12f), Is.False);

        [Test]
        public void IsAboard_OnBoundary_IsTrue()
            => Assert.That(JumpRules.IsAboard(new Vector3(12f, 0f, 0f), Vector3.zero, 12f), Is.True);
    }
}
