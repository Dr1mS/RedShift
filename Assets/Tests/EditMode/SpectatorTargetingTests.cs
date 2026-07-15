using System.Collections.Generic;
using NUnit.Framework;
using Redshift.Gameplay;

namespace Redshift.Tests.EditMode
{
    /// <summary>
    /// Cycle de cible du spectateur (SPEC §4.11, P4-11) : sélection par ClientId stable,
    /// wrap aux extrémités, repli propre quand la cible disparaît ou depuis la caméra libre.
    /// </summary>
    public class SpectatorTargetingTests
    {
        // Candidats triés croissants par ClientId (invariant fourni par l'appelant).
        private static readonly List<int> Three = new() { 0, 3, 7 };

        [Test]
        public void Next_Forward_AdvancesToHigherClientId()
            => Assert.That(SpectatorTargeting.NextClientId(Three, 0, forward: true), Is.EqualTo(3));

        [Test]
        public void Next_Forward_WrapsAtEnd()
            => Assert.That(SpectatorTargeting.NextClientId(Three, 7, forward: true), Is.EqualTo(0),
                "dernier candidat → retour au premier (cycle)");

        [Test]
        public void Next_Backward_GoesToLowerClientId()
            => Assert.That(SpectatorTargeting.NextClientId(Three, 3, forward: false), Is.EqualTo(0));

        [Test]
        public void Next_Backward_WrapsAtStart()
            => Assert.That(SpectatorTargeting.NextClientId(Three, 0, forward: false), Is.EqualTo(7),
                "premier candidat → retour au dernier (cycle arrière)");

        [Test]
        public void Next_FromFreeCamera_Forward_EntersAtFirst()
            => Assert.That(SpectatorTargeting.NextClientId(Three, SpectatorTargeting.NoTarget, forward: true), Is.EqualTo(0),
                "depuis la caméra libre, avancer entre par le premier candidat");

        [Test]
        public void Next_FromFreeCamera_Backward_EntersAtLast()
            => Assert.That(SpectatorTargeting.NextClientId(Three, SpectatorTargeting.NoTarget, forward: false), Is.EqualTo(7),
                "depuis la caméra libre, reculer entre par le dernier candidat");

        [Test]
        public void Next_CurrentTargetVanished_Forward_EntersAtFirst()
        {
            // La cible suivie (ClientId 5) s'est déconnectée : elle n'est plus dans la liste.
            // Avancer doit repartir proprement du premier candidat, pas planter.
            Assert.That(SpectatorTargeting.NextClientId(Three, 5, forward: true), Is.EqualTo(0));
        }

        [Test]
        public void Next_CurrentTargetVanished_Backward_EntersAtLast()
            => Assert.That(SpectatorTargeting.NextClientId(Three, 5, forward: false), Is.EqualTo(7));

        [Test]
        public void Next_EmptyList_ReturnsNoTarget()
            => Assert.That(SpectatorTargeting.NextClientId(new List<int>(), 0, forward: true),
                Is.EqualTo(SpectatorTargeting.NoTarget), "aucun vivant → caméra libre");

        [Test]
        public void Next_NullList_ReturnsNoTarget()
            => Assert.That(SpectatorTargeting.NextClientId(null, 0, forward: true),
                Is.EqualTo(SpectatorTargeting.NoTarget));

        [Test]
        public void Next_SingleCandidate_Forward_StaysOnIt()
            => Assert.That(SpectatorTargeting.NextClientId(new List<int> { 4 }, 4, forward: true), Is.EqualTo(4),
                "un seul vivant : cycler reste sur lui");

        [Test]
        public void Next_SingleCandidate_FromFree_EntersOnIt()
            => Assert.That(SpectatorTargeting.NextClientId(new List<int> { 4 }, SpectatorTargeting.NoTarget, forward: true),
                Is.EqualTo(4));

        [Test]
        public void IsStillValid_PresentTarget_IsTrue()
            => Assert.That(SpectatorTargeting.IsStillValid(Three, 3), Is.True);

        [Test]
        public void IsStillValid_VanishedTarget_IsFalse()
            => Assert.That(SpectatorTargeting.IsStillValid(Three, 5), Is.False,
                "cible disparue (mort/évac/déco) → invalide, déclenche le repli");

        [Test]
        public void IsStillValid_NoTargetSentinel_IsFalse()
            => Assert.That(SpectatorTargeting.IsStillValid(Three, SpectatorTargeting.NoTarget), Is.False);

        [Test]
        public void IsStillValid_EmptyList_IsFalse()
            => Assert.That(SpectatorTargeting.IsStillValid(new List<int>(), 3), Is.False);
    }
}
