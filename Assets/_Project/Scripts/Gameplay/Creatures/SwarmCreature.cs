using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Redshift.Core;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// La Nuée (SPEC §4.8) : essaim volant EXTÉRIEUR, sans NavMesh. Toute la décision vit
    /// host-side (règle absolue n°5) : le serveur simule UN seul centre d'essaim par steering
    /// aligné sur la gravité locale (<see cref="SwarmSteering"/>), l'attire vers le bruit du
    /// laser de minage (GameEvents.NoiseEmitted) et les lampes allumées, harcèle le joueur ciblé
    /// (dégâts de proximité serveur via PlayerHealth). Contre-jeu : miner par sessions courtes
    /// (l'aggro décroît vite sans stimulus) et leurres lumineux (une lampe allumée détourne).
    ///
    /// Réplication ÉCONOME (jamais un NetworkObject par boid) : NetworkTransform réplique le
    /// centre (server-auth, cadence relâchée) + UNE SyncVar&lt;byte&gt; d'état d'agitation. Les
    /// boids individuels sont du COSMÉTIQUE simulé localement chez chaque client
    /// (<see cref="SwarmBoidVisual"/>) à partir du centre + état. Aucun trafic par boid.
    /// </summary>
    [RequireComponent(typeof(GravityReceiver))]
    public class SwarmCreature : NetworkBehaviour
    {
        // États d'agitation répliqués (teinte le nuage : dispersé/calme → resserré/agité).
        public const byte StateIdle = 0;
        public const byte StateHunting = 1;
        public const byte StateHarassing = 2;

        [SerializeField] private NueeDef _def;

        private readonly SyncVar<byte> _agitation = new();

        /// <summary>État d'agitation répliqué (lu par le visuel cosmétique côté client).</summary>
        public byte Agitation => _agitation.Value;
        public NueeDef Def => _def;

        private GravityReceiver gravity;

        // --- État de simulation (host-only) ---
        private Vector3 velocity;
        private Vector3 home;
        private Vector3 wanderTarget;

        private float aggro;
        private bool engaged;
        private Vector3 lastStimulusPosition;
        private bool hasStimulus;

        private float lastBiteTime = float.NegativeInfinity;

        // Perception, réutilisée chaque tick sans réallouer (host-only, monothread).
        private PlayerHealth[] players = System.Array.Empty<PlayerHealth>();
        private float playersRefreshTimer;
        private readonly List<SwarmStimulus> _stimuli = new();

        // Ping de bruit courant (posé par l'event, consommé au tick suivant).
        private bool noisePending;
        private Vector3 noisePosition;
        private float noiseLoudness;

        private void Awake()
        {
            gravity = GetComponent<GravityReceiver>();
            // L'essaim vole par écritures de transform serveur (NetworkTransform réplique) : la
            // gravité ne doit jamais pousser un Rigidbody ici. On lit seulement l'up local.
            gravity.ApplyToBody = false;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            home = transform.position;
            wanderTarget = home;
            GameEvents.NoiseEmitted += OnNoiseEmitted;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            GameEvents.NoiseEmitted -= OnNoiseEmitted;
        }

        /// <summary>Serveur : mémorise le dernier bruit perçu (consommé au tick pour l'aggro).</summary>
        private void OnNoiseEmitted(Vector3 position, float loudness)
        {
            if (loudness <= 0f)
                return;
            float range = _def.NoiseHearingRange * Mathf.Clamp01(loudness);
            if ((position - transform.position).sqrMagnitude <= range * range)
            {
                noisePending = true;
                noisePosition = position;
                noiseLoudness = Mathf.Clamp01(loudness);
            }
        }

        private void Update()
        {
            if (!IsServerInitialized)
                return;
            gravity.Refresh();
            ServerTick(Time.deltaTime);
        }

        /// <summary>FSM de l'essaim, host-side chaque frame.</summary>
        private void ServerTick(float dt)
        {
            RefreshPlayers(dt);

            // 1) Rassembler les stimuli du tick : ping de bruit (transitoire) + lampes allumées
            //    (persistantes tant qu'IsOn). Force de la lampe pondérée par le stimulus SO.
            _stimuli.Clear();
            if (noisePending)
            {
                _stimuli.Add(new SwarmStimulus(noisePosition, _def.NoiseStimulusStrength * noiseLoudness));
                noisePending = false;
            }
            float lightSqr = _def.LightDetectionRange * _def.LightDetectionRange;
            foreach (PlayerHealth p in AlivePlayers())
            {
                var lamp = p.GetComponent<HeadLamp>();
                if (lamp == null || !lamp.IsOn)
                    continue;
                if ((p.transform.position - transform.position).sqrMagnitude <= lightSqr)
                    _stimuli.Add(new SwarmStimulus(p.transform.position, _def.LightStimulusStrength));
            }

            // 2) Choisir le stimulus dominant (plus fort d'abord, plus proche ensuite).
            int chosen = SwarmSteering.SelectTarget(_stimuli, transform.position);
            float stimulusStrength = 0f;
            if (chosen >= 0)
            {
                lastStimulusPosition = _stimuli[chosen].Position;
                stimulusStrength = _stimuli[chosen].Strength;
                hasStimulus = true;
            }

            // 3) Dynamique d'aggro (montée/décroissance + hystérésis engage/disengage).
            aggro = SwarmSteering.UpdateAggro(
                aggro, engaged, stimulusStrength,
                _def.AggroBuildRate, _def.AggroDecayRate,
                _def.EngageThreshold, _def.DisengageThreshold,
                dt, out engaged);

            // 4) Cible et altitude selon l'engagement.
            Vector3 target;
            float desiredAltitude;
            float speed;
            byte state;
            if (engaged && hasStimulus)
            {
                target = lastStimulusPosition;
                desiredAltitude = _def.HarassAltitude;
                speed = _def.HuntSpeed;
                state = StateHunting;
            }
            else
            {
                engaged = false;      // aggro retombée : on oublie le stimulus, retour errance
                hasStimulus = false;
                target = PickWanderTarget();
                desiredAltitude = _def.PatrolAltitude;
                speed = _def.PatrolSpeed;
                state = StateIdle;
            }

            // 5) Steering aligné gravité : le cœur pur calcule la nouvelle vitesse.
            Vector3 up = gravity.Up;
            float groundHeight, currentHeight;
            if (gravity.InGravity && gravity.CurrentSource != null)
            {
                Vector3 center = gravity.CurrentSource.transform.position;
                groundHeight = gravity.CurrentSource.SurfaceRadius;
                currentHeight = (transform.position - center).magnitude;
            }
            else
            {
                // Hors champ (ne devrait pas arriver en surface) : altitude neutre, pas de rappel.
                groundHeight = 0f;
                currentHeight = 0f;
                desiredAltitude = 0f;
            }

            velocity = SwarmSteering.ComputeVelocity(
                transform.position, velocity, target, up,
                desiredAltitude, groundHeight, currentHeight,
                speed, _def.MaxAcceleration, _def.AltitudeStiffness, dt);

            transform.position += velocity * dt;
            if (velocity.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized, up);

            // 6) Harcèlement : dégâts de proximité (décidés serveur, depuis distance centre↔joueur).
            if (engaged)
                TryBite(ref state);

            SetAgitation(state);
        }

        /// <summary>Errance : rejoint un point, en repioche un nouveau une fois arrivé.</summary>
        private Vector3 PickWanderTarget()
        {
            if ((transform.position - wanderTarget).sqrMagnitude <= _def.ArriveRadius * _def.ArriveRadius)
            {
                Vector3 up = gravity.Up;
                // Un décalage tangentiel autour du point d'ancrage (à plat sur la surface).
                Vector3 random = Random.insideUnitSphere * _def.PatrolRadius;
                wanderTarget = home + Vector3.ProjectOnPlane(random, up);
            }
            return wanderTarget;
        }

        /// <summary>Dégâts de proximité au joueur le plus proche dans le rayon (serveur).</summary>
        private void TryBite(ref byte state)
        {
            PlayerHealth victim = null;
            float bestSqr = _def.DamageRadius * _def.DamageRadius;
            foreach (PlayerHealth p in AlivePlayers())
            {
                float sqr = (p.transform.position - transform.position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    victim = p;
                }
            }
            if (victim == null)
                return;

            state = StateHarassing;
            if (Time.time - lastBiteTime >= _def.DamageInterval)
            {
                lastBiteTime = Time.time;
                victim.ApplyDamage(_def.DamagePerHit);
            }
        }

        private void RefreshPlayers(float dt)
        {
            playersRefreshTimer -= dt;
            if (playersRefreshTimer <= 0f)
            {
                playersRefreshTimer = 2f;
                players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
            }
        }

        private IEnumerable<PlayerHealth> AlivePlayers()
        {
            foreach (PlayerHealth p in players)
                if (p != null && !p.IsDead && !p.IsEvacuated)
                    yield return p;
        }

        private void SetAgitation(byte state)
        {
            if (IsServerInitialized && _agitation.Value != state)
                _agitation.Value = state;
        }
    }
}
