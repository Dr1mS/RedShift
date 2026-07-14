using System.Collections.Generic;
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

        // Garde téléport (dette P3) : un saut de portail ne doit pas être « entendu » comme du bruit.
        [Test]
        public void ContinuousMotion_SmallFrameStep_IsContinuous()
            => Assert.That(CreaturePerception.IsContinuousMotion(0.5f), Is.True, "un pas de frame normal compte comme bruit continu");

        [Test]
        public void ContinuousMotion_FastLegitMove_IsContinuous()
            => Assert.That(CreaturePerception.IsContinuousMotion(2f), Is.True, "navette rapide (~2 m/frame) reste du mouvement continu");

        [Test]
        public void ContinuousMotion_PortalJump_IsIgnored()
            => Assert.That(CreaturePerception.IsContinuousMotion(2000f), Is.False, "traversée de portail (~2000 m) : téléport, pas du bruit");

        [Test]
        public void ContinuousMotion_JustBelowGuard_IsContinuous()
            => Assert.That(CreaturePerception.IsContinuousMotion(CreaturePerception.TeleportGuardDistance - 0.1f), Is.True);

        [Test]
        public void ContinuousMotion_AtGuard_IsIgnored()
            => Assert.That(CreaturePerception.IsContinuousMotion(CreaturePerception.TeleportGuardDistance), Is.False, "seuil inclusif : au garde, on ignore");

        // --- Ciblage « joueur isolé » du Pâle (SPEC §4.8, P4-8) ---
        // IsolationRadius de test : 20 m (aligné sur SO_Creature_Pale).
        private const float Isolation = 20f;

        [Test]
        public void Isolation_LonePlayer_IsIsolated()
        {
            var alive = new List<Vector3> { new Vector3(0f, 0f, 0f) };
            Assert.That(CreaturePerception.IsIsolated(alive, 0, Isolation), Is.True,
                "un joueur seul est isolé par définition — le Pâle reste dangereux en solo");
        }

        [Test]
        public void Isolation_TwoNearbyPlayers_NeitherIsolated()
        {
            // Deux joueurs à 5 m (< 20) : chacun a un voisin proche → aucun isolé.
            var alive = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f) };
            Assert.That(CreaturePerception.IsIsolated(alive, 0, Isolation), Is.False);
            Assert.That(CreaturePerception.IsIsolated(alive, 1, Isolation), Is.False);
        }

        [Test]
        public void Isolation_TwoNearby_SelectFallsBackToNearest()
        {
            // Aucun isolé (5 m d'écart) : repli sur le plus proche de l'observateur.
            // Observateur en (0,0,-3) : le joueur 0 (dist 3) est plus proche que le 1 (dist ~5.8).
            var alive = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f) };
            var candidates = new List<int> { 0, 1 };
            int chosen = CreaturePerception.SelectStalkTarget(alive, candidates, new Vector3(0f, 0f, -3f), Isolation);
            Assert.That(chosen, Is.EqualTo(0), "aucun isolé → plus proche de l'observateur");
        }

        [Test]
        public void Isolation_ThreePlayers_IsolatedOneTargetedEvenIfNotNearest()
        {
            // Joueurs 0 et 1 groupés près de l'observateur (2 m d'écart) ; joueur 2 écarté à 40 m
            // des autres mais visible. L'observateur est proche du groupe. Le 2 (isolé) doit être
            // ciblé même s'il est le PLUS LOIN de l'observateur.
            var alive = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),   // groupé
                new Vector3(2f, 0f, 0f),   // groupé (voisin du 0)
                new Vector3(0f, 0f, 40f),  // écarté → isolé
            };
            var candidates = new List<int> { 0, 1, 2 };
            int chosen = CreaturePerception.SelectStalkTarget(alive, candidates, new Vector3(0f, 0f, -1f), Isolation);
            Assert.That(chosen, Is.EqualTo(2),
                "le joueur écarté (isolé) est traqué en priorité, même le plus loin");
        }

        [Test]
        public void Isolation_DeadPlayersIgnoredInNeighborhood()
        {
            // Le voisinage ne considère que les positions VIVANTES fournies : les morts ne sont
            // pas dans la liste. Ici un seul vivant présent (un mort proche a été filtré en amont)
            // → il est isolé et donc ciblé bien qu'aucun autre vivant ne soit là.
            var alive = new List<Vector3> { new Vector3(0f, 0f, 0f) }; // le mort à 3 m n'est PAS listé
            var candidates = new List<int> { 0 };
            Assert.That(CreaturePerception.IsIsolated(alive, 0, Isolation), Is.True,
                "un mort proche (non listé) ne « désisole » pas le vivant");
            int chosen = CreaturePerception.SelectStalkTarget(alive, candidates, new Vector3(0f, 0f, -5f), Isolation);
            Assert.That(chosen, Is.EqualTo(0));
        }

        [Test]
        public void Isolation_NoCandidates_ReturnsMinusOne()
        {
            var alive = new List<Vector3> { new Vector3(0f, 0f, 0f) };
            int chosen = CreaturePerception.SelectStalkTarget(alive, new List<int>(), Vector3.zero, Isolation);
            Assert.That(chosen, Is.EqualTo(-1), "aucun candidat visible → pas de cible");
        }

        [Test]
        public void Isolation_NonCandidateNeighborStillProtects()
        {
            // Câblage-clé (advisor) : le voisinage inclut TOUS les vivants, pas seulement les
            // candidats. Joueur 0 (candidat, visible) collé au joueur 1 (NON candidat, ex. sans
            // LOS vers la créature). Le 0 n'est donc PAS isolé → repli sur plus proche (ici le 0
            // reste le seul candidat).
            var alive = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f) };
            Assert.That(CreaturePerception.IsIsolated(alive, 0, Isolation), Is.False,
                "un voisin non-candidat protège quand même");
            var candidates = new List<int> { 0 }; // seul le 0 est visible
            int chosen = CreaturePerception.SelectStalkTarget(alive, candidates, new Vector3(0f, 0f, -2f), Isolation);
            Assert.That(chosen, Is.EqualTo(0), "0 non isolé mais seul candidat → repli");
        }

        [Test]
        public void Isolation_AtRadiusBoundary_IsIsolated()
        {
            // Frontière : voisin exactement à IsolationRadius → « à moins de » est strict, donc isolé.
            var alive = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(Isolation, 0f, 0f) };
            Assert.That(CreaturePerception.IsIsolated(alive, 0, Isolation), Is.True,
                "voisin pile à la frontière : pas « à moins de » → isolé");
        }
    }
}
