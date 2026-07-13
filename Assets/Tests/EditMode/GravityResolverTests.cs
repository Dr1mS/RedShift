using System.Collections.Generic;
using NUnit.Framework;
using Redshift.Gameplay;
using UnityEngine;

namespace Redshift.Tests.EditMode
{
    /// <summary>Règles de sélection de champ de gravité (SPEC §4.2) : priorité, proximité, hystérésis.</summary>
    public class GravityResolverTests
    {
        private const float Hysteresis = 0.1f;

        private static GravityFieldData Planet(int id, Vector3 center, float surface = 200f, float influence = 400f, int priority = 0)
            => new(id, center, surface, influence, 9.81f, priority);

        [Test]
        public void NoFields_ReturnsNoField()
        {
            int result = GravityResolver.Resolve(new List<GravityFieldData>(), Vector3.zero, GravityResolver.NoField, Hysteresis);
            Assert.That(result, Is.EqualTo(GravityResolver.NoField));
        }

        [Test]
        public void InsideSingleField_ReturnsIt()
        {
            var fields = new List<GravityFieldData> { Planet(1, Vector3.zero) };
            int result = GravityResolver.Resolve(fields, new Vector3(0f, 210f, 0f), GravityResolver.NoField, Hysteresis);
            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void OutsideInfluence_ReturnsNoField()
        {
            var fields = new List<GravityFieldData> { Planet(1, Vector3.zero) };
            int result = GravityResolver.Resolve(fields, new Vector3(0f, 500f, 0f), GravityResolver.NoField, Hysteresis);
            Assert.That(result, Is.EqualTo(GravityResolver.NoField));
        }

        [Test]
        public void HigherPriority_WinsInsideOverlap()
        {
            var planet = Planet(1, Vector3.zero);
            var moon = Planet(2, new Vector3(300f, 0f, 0f), surface: 40f, influence: 80f, priority: 1);
            var fields = new List<GravityFieldData> { planet, moon };

            int result = GravityResolver.Resolve(fields, new Vector3(300f, 45f, 0f), 1, Hysteresis);
            Assert.That(result, Is.EqualTo(2), "La lune (priorité 1) doit prendre la main sur la planète, même avec hystérésis en faveur du courant.");
        }

        [Test]
        public void Hysteresis_CurrentSurvivesJustBeyondBoundary()
        {
            var fields = new List<GravityFieldData> { Planet(1, Vector3.zero) };
            var justOutside = new Vector3(0f, 400f * 1.05f, 0f);

            Assert.That(GravityResolver.Resolve(fields, justOutside, 1, Hysteresis), Is.EqualTo(1),
                "À 1.05×influence, le champ courant doit être conservé (bande d'hystérésis).");
            Assert.That(GravityResolver.Resolve(fields, justOutside, GravityResolver.NoField, Hysteresis), Is.EqualTo(GravityResolver.NoField),
                "Le même point sans champ courant ne doit PAS accrocher le champ (entrée stricte).");
        }

        [Test]
        public void Hysteresis_CurrentLostFarBeyondBoundary()
        {
            var fields = new List<GravityFieldData> { Planet(1, Vector3.zero) };
            int result = GravityResolver.Resolve(fields, new Vector3(0f, 400f * 1.2f, 0f), 1, Hysteresis);
            Assert.That(result, Is.EqualTo(GravityResolver.NoField));
        }

        [Test]
        public void EqualPriority_NearMidpoint_NoFlipFlop()
        {
            var a = Planet(1, Vector3.zero);
            var b = Planet(2, new Vector3(500f, 0f, 0f));
            var fields = new List<GravityFieldData> { a, b };
            // Légèrement plus proche de B, mais sous la marge d'hystérésis (0.1 × 400 = 40 m).
            var nearMidpoint = new Vector3(260f, 0f, 0f);

            Assert.That(GravityResolver.Resolve(fields, nearMidpoint, 1, Hysteresis), Is.EqualTo(1),
                "A courant : reste A malgré B légèrement plus proche.");
            Assert.That(GravityResolver.Resolve(fields, nearMidpoint, 2, Hysteresis), Is.EqualTo(2),
                "B courant : reste B — pas d'oscillation possible au voisinage du point d'équilibre.");
        }

        [Test]
        public void EqualPriority_ClearlyCloserChallenger_TakesOver()
        {
            var a = Planet(1, Vector3.zero);
            var b = Planet(2, new Vector3(500f, 0f, 0f));
            var fields = new List<GravityFieldData> { a, b };
            // Franchement du côté de B : au-delà de la marge de 40 m.
            var nearB = new Vector3(340f, 0f, 0f);

            Assert.That(GravityResolver.Resolve(fields, nearB, 1, Hysteresis), Is.EqualTo(2));
        }

        [Test]
        public void Resolution_IsIndependentOfFieldOrder()
        {
            var a = Planet(1, Vector3.zero);
            var b = Planet(2, new Vector3(500f, 0f, 0f));
            var nearMidpoint = new Vector3(260f, 0f, 0f);

            int resultAB = GravityResolver.Resolve(new List<GravityFieldData> { a, b }, nearMidpoint, 1, Hysteresis);
            int resultBA = GravityResolver.Resolve(new List<GravityFieldData> { b, a }, nearMidpoint, 1, Hysteresis);
            Assert.That(resultAB, Is.EqualTo(resultBA));
        }
    }
}
