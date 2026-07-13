using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Redshift.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Redshift.Tests.PlayMode
{
    /// <summary>Tests PlayMode gravité exigés par le gate G1 : chute, micro-gravité, hystérésis sans oscillation.</summary>
    public class GravityPlayModeTests
    {
        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                if (go != null)
                    Object.DestroyImmediate(go);
            spawned.Clear();
            GravitySource.InvalidateCache();
        }

        private GravitySource CreateSource(string name, Vector3 position, float surface, float influence, int priority = 0, float g = 9.81f)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            spawned.Add(go);
            var source = go.AddComponent<GravitySource>();
            source.Configure(surface, influence, g, priority);
            return source;
        }

        private GravityReceiver CreateReceiver(Vector3 position, bool withRigidbody)
        {
            var go = new GameObject("Receiver");
            go.transform.position = position;
            spawned.Add(go);
            if (withRigidbody)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.useGravity = false;
            }
            return go.AddComponent<GravityReceiver>();
        }

        [UnityTest]
        public IEnumerator Receiver_FallsTowardSourceCenter()
        {
            CreateSource("Planet", Vector3.zero, surface: 10f, influence: 100f);
            GravityReceiver receiver = CreateReceiver(new Vector3(0f, 30f, 0f), withRigidbody: true);
            var body = receiver.GetComponent<Rigidbody>();

            for (int i = 0; i < 10; i++)
                yield return new WaitForFixedUpdate();

            Vector3 towardCenter = (Vector3.zero - receiver.transform.position).normalized;
            Assert.That(receiver.InGravity, Is.True);
            Assert.That(Vector3.Dot(body.linearVelocity.normalized, towardCenter), Is.GreaterThan(0.99f),
                "Le corps doit tomber droit vers le centre de la source.");
        }

        [UnityTest]
        public IEnumerator Receiver_OutsideInfluence_IsInMicroGravity()
        {
            CreateSource("Planet", Vector3.zero, surface: 10f, influence: 50f);
            GravityReceiver receiver = CreateReceiver(new Vector3(0f, 200f, 0f), withRigidbody: true);
            var body = receiver.GetComponent<Rigidbody>();

            for (int i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(receiver.InGravity, Is.False);
            Assert.That(receiver.CurrentAcceleration, Is.EqualTo(Vector3.zero));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(1e-3f), "Micro-gravité : aucune force appliquée.");
        }

        [UnityTest]
        public IEnumerator Receiver_NoOscillationAtEqualPriorityBoundary()
        {
            GravitySource a = CreateSource("A", Vector3.zero, surface: 200f, influence: 400f);
            GravitySource b = CreateSource("B", new Vector3(500f, 0f, 0f), surface: 200f, influence: 400f);
            GravityReceiver receiver = CreateReceiver(new Vector3(100f, 0f, 0f), withRigidbody: false);

            yield return new WaitForFixedUpdate();
            Assert.That(receiver.CurrentSource, Is.EqualTo(a), "Départ clairement côté A.");

            // Oscillation de position autour du point d'équilibre, dans la marge d'hystérésis (40 m).
            int switches = 0;
            GravitySource last = receiver.CurrentSource;
            float[] xs = { 245f, 255f, 248f, 252f, 246f, 254f, 250f };
            foreach (float x in xs)
            {
                receiver.transform.position = new Vector3(x, 0f, 0f);
                yield return new WaitForFixedUpdate();
                if (receiver.CurrentSource != last)
                {
                    switches++;
                    last = receiver.CurrentSource;
                }
            }
            Assert.That(switches, Is.Zero, "Aucun changement de champ dans la bande d'hystérésis (SPEC §4.2).");
            Assert.That(receiver.CurrentSource, Is.EqualTo(a));

            // Franchement côté B : un seul basculement, définitif.
            receiver.transform.position = new Vector3(340f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            Assert.That(receiver.CurrentSource, Is.EqualTo(b), "Bien au-delà de la marge, le champ B prend la main.");
        }

        [UnityTest]
        public IEnumerator Receiver_HigherPrioritySource_TakesOver()
        {
            CreateSource("Planet", Vector3.zero, surface: 200f, influence: 400f, priority: 0);
            GravitySource moon = CreateSource("Moon", new Vector3(300f, 0f, 0f), surface: 40f, influence: 80f, priority: 1);
            GravityReceiver receiver = CreateReceiver(new Vector3(100f, 0f, 0f), withRigidbody: false);

            yield return new WaitForFixedUpdate();
            Assert.That(receiver.CurrentSource.Priority, Is.EqualTo(0));

            receiver.transform.position = new Vector3(300f, 50f, 0f);
            yield return new WaitForFixedUpdate();
            Assert.That(receiver.CurrentSource, Is.EqualTo(moon),
                "Dans l'influence de la lune (priorité 1), elle domine la planète malgré l'hystérésis.");
        }
    }
}
