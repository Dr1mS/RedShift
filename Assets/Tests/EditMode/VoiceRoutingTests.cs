using System.Collections.Generic;
using NUnit.Framework;
using Redshift.Gameplay;
using Channel = Redshift.Gameplay.VoiceRouting.Channel;

namespace Redshift.Tests.EditMode
{
    /// <summary>
    /// Matrice de routage vocal (SPEC §5 voice + §4.11 canal morts, P4-13) : produit croisé
    /// vivant/mort × radio ON/OFF, invariant « on ne transmet que dans un canal rejoint », et
    /// diffs purs d'appartenance (join/leave) utilisés par la réconciliation du service.
    /// </summary>
    public class VoiceRoutingTests
    {
        // --- Produit croisé des règles ---

        [Test]
        public void Alive_RadioOff_JoinsPositionalAndTransmitsPositional()
        {
            var d = VoiceRouting.Resolve(isDead: false, radioTransmit: false);
            Assert.That(d.IsMember(Channel.Positional), Is.True);
            Assert.That(d.IsMember(Channel.Radio), Is.False);
            Assert.That(d.IsMember(Channel.Dead), Is.False);
            Assert.That(d.Transmit, Is.EqualTo(Channel.Positional));
        }

        [Test]
        public void Alive_RadioOn_JoinsPositionalAndRadio_TransmitsRadio()
        {
            var d = VoiceRouting.Resolve(isDead: false, radioTransmit: true);
            Assert.That(d.IsMember(Channel.Positional), Is.True, "reste en écoute du positionnel");
            Assert.That(d.IsMember(Channel.Radio), Is.True);
            Assert.That(d.IsMember(Channel.Dead), Is.False);
            Assert.That(d.Transmit, Is.EqualTo(Channel.Radio), "la voix part sur la radio, pas sur le positionnel");
        }

        [Test]
        public void Dead_JoinsDeadOnly_TransmitsDead_LeavesPositional()
        {
            var d = VoiceRouting.Resolve(isDead: true, radioTransmit: false);
            Assert.That(d.IsMember(Channel.Dead), Is.True);
            Assert.That(d.Transmit, Is.EqualTo(Channel.Dead));
            // SPEC §4.11 : canal morts séparé — pas de bleed mort→vivants (le mort quitte le positionnel).
            Assert.That(d.IsMember(Channel.Positional), Is.EqualTo(VoiceRouting.DeadHearsLiving),
                "les morts n'entendent pas les vivants tant que DeadHearsLiving est faux (lecture SPEC)");
        }

        [Test]
        public void Dead_IgnoresRadio_CorpseHoldsNoTalkie()
        {
            // Un cadavre ne tient pas de talkie : radio ignorée à la mort.
            var d = VoiceRouting.Resolve(isDead: true, radioTransmit: true);
            Assert.That(d.IsMember(Channel.Radio), Is.False);
            Assert.That(d.Transmit, Is.EqualTo(Channel.Dead));
        }

        // --- Invariant : la transmission est toujours dans l'appartenance ---

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Transmit_IsAlwaysWithinMembership(bool isDead, bool radio)
        {
            var d = VoiceRouting.Resolve(isDead, radio);
            if (d.Transmit != Channel.None)
                Assert.That(d.IsMember(d.Transmit), Is.True,
                    "on ne peut pas transmettre dans un canal non rejoint");
        }

        // --- Diffs purs d'appartenance (réconciliation) ---

        [Test]
        public void ChannelsToJoin_FromPositional_ToDead_JoinsDeadOnly()
        {
            var current = new List<Channel> { Channel.Positional };
            var desired = new List<Channel> { Channel.Dead };
            Assert.That(VoiceRouting.ChannelsToJoin(current, desired), Is.EquivalentTo(new[] { Channel.Dead }));
        }

        [Test]
        public void ChannelsToLeave_FromPositional_ToDead_LeavesPositional()
        {
            var current = new List<Channel> { Channel.Positional };
            var desired = new List<Channel> { Channel.Dead };
            Assert.That(VoiceRouting.ChannelsToLeave(current, desired), Is.EquivalentTo(new[] { Channel.Positional }));
        }

        [Test]
        public void ChannelsToJoin_AddingRadio_KeepsPositional_JoinsRadioOnly()
        {
            var current = new List<Channel> { Channel.Positional };
            var desired = new List<Channel> { Channel.Positional, Channel.Radio };
            Assert.That(VoiceRouting.ChannelsToJoin(current, desired), Is.EquivalentTo(new[] { Channel.Radio }));
            Assert.That(VoiceRouting.ChannelsToLeave(current, desired), Is.Empty, "le positionnel est conservé");
        }

        [Test]
        public void ChannelsToLeave_DroppingRadio_LeavesRadioOnly()
        {
            var current = new List<Channel> { Channel.Positional, Channel.Radio };
            var desired = new List<Channel> { Channel.Positional };
            Assert.That(VoiceRouting.ChannelsToLeave(current, desired), Is.EquivalentTo(new[] { Channel.Radio }));
        }

        [Test]
        public void ChannelsToJoin_AlreadyMatching_JoinsNothing()
        {
            var current = new List<Channel> { Channel.Positional };
            var desired = new List<Channel> { Channel.Positional };
            Assert.That(VoiceRouting.ChannelsToJoin(current, desired), Is.Empty);
            Assert.That(VoiceRouting.ChannelsToLeave(current, desired), Is.Empty);
        }

        [Test]
        public void ChannelsToJoin_FromEmpty_JoinsAllDesired()
        {
            var desired = new List<Channel> { Channel.Positional, Channel.Radio };
            Assert.That(VoiceRouting.ChannelsToJoin(new List<Channel>(), desired),
                Is.EquivalentTo(new[] { Channel.Positional, Channel.Radio }));
        }

        [Test]
        public void ChannelsDiff_NullInputs_AreSafe()
        {
            Assert.That(VoiceRouting.ChannelsToJoin(null, null), Is.Empty);
            Assert.That(VoiceRouting.ChannelsToLeave(null, null), Is.Empty);
            var desired = new List<Channel> { Channel.Positional };
            Assert.That(VoiceRouting.ChannelsToJoin(null, desired), Is.EquivalentTo(new[] { Channel.Positional }));
            Assert.That(VoiceRouting.ChannelsToLeave(desired, null), Is.EquivalentTo(new[] { Channel.Positional }));
        }

        [Test]
        public void ChannelsDiff_IgnoresNoneSentinel()
        {
            var current = new List<Channel> { Channel.None };
            var desired = new List<Channel> { Channel.None };
            Assert.That(VoiceRouting.ChannelsToJoin(current, desired), Is.Empty);
            Assert.That(VoiceRouting.ChannelsToLeave(current, desired), Is.Empty);
        }

        // --- Scénario complet : cycle vie → mort → retour à la vie ---

        [Test]
        public void FullTransition_AliveToDeadToAlive_ReconcilesCorrectly()
        {
            // Vivant.
            var alive = VoiceRouting.Resolve(false, false);
            Assert.That(alive.Membership, Is.EquivalentTo(new[] { Channel.Positional }));

            // Mort : quitte positionnel, rejoint morts.
            var dead = VoiceRouting.Resolve(true, false);
            Assert.That(VoiceRouting.ChannelsToLeave(alive.Membership, dead.Membership),
                Is.EquivalentTo(new[] { Channel.Positional }));
            Assert.That(VoiceRouting.ChannelsToJoin(alive.Membership, dead.Membership),
                Is.EquivalentTo(new[] { Channel.Dead }));

            // Retour à la vie (système futur) : quitte morts, rejoint positionnel.
            var revived = VoiceRouting.Resolve(false, false);
            Assert.That(VoiceRouting.ChannelsToLeave(dead.Membership, revived.Membership),
                Is.EquivalentTo(new[] { Channel.Dead }));
            Assert.That(VoiceRouting.ChannelsToJoin(dead.Membership, revived.Membership),
                Is.EquivalentTo(new[] { Channel.Positional }));
        }
    }
}
