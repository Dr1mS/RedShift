using System.Collections.Generic;
using NUnit.Framework;
using Redshift.Gameplay;
using UnityEngine;

namespace Redshift.Tests.EditMode
{
    /// <summary>
    /// Cœur logique de la Nuée (SPEC §4.8) : steering aligné gravité, dynamique d'aggro
    /// (montée/décroissance/hystérésis, contre-jeu « sessions courtes ») et choix de cible
    /// entre stimuli bruit/lumière. Logique pure — aucune scène.
    /// </summary>
    public class SwarmSteeringTests
    {
        // --- Steering ---

        [Test]
        public void Velocity_ClampedToMaxSpeed()
        {
            // Cible très loin à plat : la vitesse désirée sature à maxSpeed.
            Vector3 v = SwarmSteering.ComputeVelocity(
                position: Vector3.zero, velocity: Vector3.zero,
                target: new Vector3(1000f, 0f, 0f), localUp: Vector3.up,
                desiredAltitude: 0f, groundHeightAlongUp: 0f, currentHeightAlongUp: 0f,
                maxSpeed: 10f, maxAccel: 100f, altitudeStiffness: 1f, dt: 1f);
            Assert.That(v.magnitude, Is.LessThanOrEqualTo(10f + 1e-3f), "vitesse bornée à maxSpeed");
        }

        [Test]
        public void Velocity_AccelerationClamped()
        {
            // maxAccel faible : sur 1 s, la vitesse ne peut pas dépasser maxAccel*dt.
            Vector3 v = SwarmSteering.ComputeVelocity(
                Vector3.zero, Vector3.zero, new Vector3(1000f, 0f, 0f), Vector3.up,
                0f, 0f, 0f, maxSpeed: 100f, maxAccel: 3f, altitudeStiffness: 1f, dt: 1f);
            Assert.That(v.magnitude, Is.LessThanOrEqualTo(3f + 1e-3f), "accélération bornée à maxAccel*dt");
        }

        [Test]
        public void Velocity_MovesTangentiallyTowardTargetOnFlatGround()
        {
            // Cible plein +X, à l'altitude déjà tenue : la vitesse part vers +X.
            Vector3 v = SwarmSteering.ComputeVelocity(
                Vector3.zero, Vector3.zero, new Vector3(50f, 0f, 0f), Vector3.up,
                desiredAltitude: 0f, groundHeightAlongUp: 0f, currentHeightAlongUp: 0f,
                maxSpeed: 10f, maxAccel: 100f, altitudeStiffness: 1f, dt: 1f);
            Assert.That(v.x, Is.GreaterThan(0f), "avance vers la cible");
            Assert.That(Mathf.Abs(v.y), Is.LessThan(1e-3f), "pas de composante verticale : altitude déjà tenue");
        }

        [Test]
        public void Velocity_RisesWhenBelowDesiredAltitude()
        {
            // Sol à 0, altitude désirée 10, actuellement à 2 → doit monter (+Y).
            Vector3 v = SwarmSteering.ComputeVelocity(
                Vector3.zero, Vector3.zero, Vector3.zero, Vector3.up,
                desiredAltitude: 10f, groundHeightAlongUp: 0f, currentHeightAlongUp: 2f,
                maxSpeed: 10f, maxAccel: 100f, altitudeStiffness: 2f, dt: 1f);
            Assert.That(v.y, Is.GreaterThan(0f), "sous l'altitude cible → l'essaim monte");
        }

        [Test]
        public void Velocity_DescendsWhenAboveDesiredAltitude()
        {
            // Actuellement à 20, cible 5 → doit descendre (-Y).
            Vector3 v = SwarmSteering.ComputeVelocity(
                Vector3.zero, Vector3.zero, Vector3.zero, Vector3.up,
                desiredAltitude: 5f, groundHeightAlongUp: 0f, currentHeightAlongUp: 20f,
                maxSpeed: 10f, maxAccel: 100f, altitudeStiffness: 2f, dt: 1f);
            Assert.That(v.y, Is.LessThan(0f), "au-dessus de l'altitude cible → l'essaim descend");
        }

        [Test]
        public void Velocity_AlignsAltitudeWithArbitraryUp()
        {
            // Up local = +X (côté d'une planète) : le rappel d'altitude doit se faire le long de +X.
            Vector3 up = Vector3.right;
            Vector3 v = SwarmSteering.ComputeVelocity(
                Vector3.zero, Vector3.zero, Vector3.zero, up,
                desiredAltitude: 10f, groundHeightAlongUp: 0f, currentHeightAlongUp: 2f,
                maxSpeed: 10f, maxAccel: 100f, altitudeStiffness: 2f, dt: 1f);
            Assert.That(v.x, Is.GreaterThan(0f), "le rappel d'altitude suit l'up local (+X), pas le +Y monde");
        }

        [Test]
        public void Velocity_ZeroDeltaTime_ReturnsUnchanged()
        {
            Vector3 start = new Vector3(1f, 2f, 3f);
            Vector3 v = SwarmSteering.ComputeVelocity(
                Vector3.zero, start, new Vector3(50f, 0f, 0f), Vector3.up,
                0f, 0f, 0f, 10f, 100f, 1f, dt: 0f);
            Assert.That(v, Is.EqualTo(start), "dt=0 : pas de pas d'intégration");
        }

        // --- Aggro : montée, décroissance, hystérésis ---

        private static float Build(float aggro, bool wasEngaged, float stim, float dt, out bool engaged)
            => SwarmSteering.UpdateAggro(aggro, wasEngaged, stim,
                buildRate: 2.5f, decayRate: 0.25f, engageThreshold: 0.5f, disengageThreshold: 0.2f,
                dt: dt, out engaged);

        [Test]
        public void Aggro_BuildsTowardStimulus()
        {
            float a = Build(0f, false, 1f, 0.1f, out _);
            Assert.That(a, Is.EqualTo(0.25f).Within(1e-4f), "monte à buildRate*dt (2.5*0.1)");
        }

        [Test]
        public void Aggro_EngagesWhenAboveThreshold()
        {
            // 0.5 requis pour s'engager : à 0.25*dt il faut plusieurs pas. On force 0.6 direct.
            Build(0.6f, false, 1f, 0f, out bool engaged);
            Assert.That(engaged, Is.True, "aggro >= EngageThreshold → engagé");
        }

        [Test]
        public void Aggro_DoesNotEngageBelowThreshold()
        {
            Build(0.4f, false, 1f, 0f, out bool engaged);
            Assert.That(engaged, Is.False, "aggro < EngageThreshold et pas déjà engagé → pas engagé");
        }

        [Test]
        public void Aggro_HysteresisStaysEngagedBetweenThresholds()
        {
            // Déjà engagé, aggro retombée à 0.35 (entre 0.2 et 0.5) → reste engagé (hystérésis).
            Build(0.35f, true, 0f, 0f, out bool engaged);
            Assert.That(engaged, Is.True, "hystérésis : engagé tant qu'au-dessus du seuil bas");
        }

        [Test]
        public void Aggro_DisengagesBelowLowThreshold()
        {
            // Déjà engagé mais aggro sous DisengageThreshold (0.2) → se désengage.
            Build(0.15f, true, 0f, 0f, out bool engaged);
            Assert.That(engaged, Is.False, "sous DisengageThreshold → désengagement");
        }

        [Test]
        public void Aggro_DecaysWhenNoStimulus()
        {
            // Contre-jeu « sessions courtes » : sans stimulus, l'aggro décroît.
            float a = Build(0.8f, true, 0f, 1f, out _);
            Assert.That(a, Is.EqualTo(0.55f).Within(1e-4f), "décroît à decayRate*dt (0.25*1)");
        }

        [Test]
        public void Aggro_SourceDisappears_EventuallyDisengages()
        {
            // Scénario complet : engagé à fond, source coupée, on laisse décroître jusqu'au lâcher.
            float a = 1f;
            bool engaged = true;
            for (int i = 0; i < 100 && engaged; i++)
                a = Build(a, engaged, 0f, 0.5f, out engaged);
            Assert.That(engaged, Is.False, "source disparue → l'essaim finit par se désengager");
            Assert.That(a, Is.LessThan(0.2f), "aggro retombée sous le seuil bas");
        }

        [Test]
        public void Aggro_ClampedTo01()
        {
            float a = Build(0.95f, true, 1f, 1f, out _);
            Assert.That(a, Is.LessThanOrEqualTo(1f), "aggro bornée à 1");
            float b = Build(0.05f, true, 0f, 1f, out _);
            Assert.That(b, Is.GreaterThanOrEqualTo(0f), "aggro bornée à 0");
        }

        // --- Choix de cible entre stimuli ---

        [Test]
        public void SelectTarget_Empty_ReturnsMinusOne()
            => Assert.That(SwarmSteering.SelectTarget(new List<SwarmStimulus>(), Vector3.zero), Is.EqualTo(-1));

        [Test]
        public void SelectTarget_StrongerWins()
        {
            var stimuli = new List<SwarmStimulus>
            {
                new SwarmStimulus(new Vector3(1f, 0f, 0f), 0.4f),   // proche mais faible (lampe)
                new SwarmStimulus(new Vector3(50f, 0f, 0f), 0.9f),  // loin mais fort (minage)
            };
            Assert.That(SwarmSteering.SelectTarget(stimuli, Vector3.zero), Is.EqualTo(1),
                "le stimulus le plus fort l'emporte même plus loin (minage > lampe)");
        }

        [Test]
        public void SelectTarget_EqualStrength_NearestWins()
        {
            var stimuli = new List<SwarmStimulus>
            {
                new SwarmStimulus(new Vector3(30f, 0f, 0f), 0.8f),
                new SwarmStimulus(new Vector3(5f, 0f, 0f), 0.8f),
            };
            Assert.That(SwarmSteering.SelectTarget(stimuli, Vector3.zero), Is.EqualTo(1),
                "à force égale, le plus proche du centre de l'essaim");
        }

        [Test]
        public void SelectTarget_ZeroStrengthIgnored()
        {
            var stimuli = new List<SwarmStimulus>
            {
                new SwarmStimulus(new Vector3(1f, 0f, 0f), 0f),   // force nulle → ignoré
                new SwarmStimulus(new Vector3(50f, 0f, 0f), 0.3f),
            };
            Assert.That(SwarmSteering.SelectTarget(stimuli, Vector3.zero), Is.EqualTo(1));
        }

        [Test]
        public void SelectTarget_AllZeroStrength_ReturnsMinusOne()
        {
            var stimuli = new List<SwarmStimulus>
            {
                new SwarmStimulus(new Vector3(1f, 0f, 0f), 0f),
                new SwarmStimulus(new Vector3(2f, 0f, 0f), 0f),
            };
            Assert.That(SwarmSteering.SelectTarget(stimuli, Vector3.zero), Is.EqualTo(-1));
        }
    }
}
