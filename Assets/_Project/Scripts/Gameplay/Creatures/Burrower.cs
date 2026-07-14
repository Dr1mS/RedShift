using Redshift.Core;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Le Fouisseur (SPEC §4.8) : patrouille les ruines, aveugle — il ne réagit qu'au bruit
    /// (pas de vue, la lampe est inutile contre lui). Contre-jeu : s'accroupir/ralentir
    /// (silencieux) et le contourner. Écoute aussi les événements de bruit (minage).
    /// </summary>
    public class Burrower : Creature
    {
        private Vector3 lastHeardPosition;
        private float memoryTimer;

        public override void OnStartServer()
        {
            base.OnStartServer();
            GameEvents.NoiseEmitted += OnNoiseEmitted;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            GameEvents.NoiseEmitted -= OnNoiseEmitted;
        }

        private void OnNoiseEmitted(Vector3 position, float loudness)
        {
            if (Vector3.Distance(transform.position, position) <= _def.NoiseHearingRange * Mathf.Clamp01(loudness))
                Hear(position);
        }

        private void Hear(Vector3 position)
        {
            lastHeardPosition = position;
            memoryTimer = _def.MemorySeconds;
        }

        protected override void ServerTick(float dt)
        {
            // Ouïe passive : la portée dépend de la vitesse observée du joueur (accroupi = silencieux).
            foreach (PlayerHealth p in AlivePlayers())
            {
                float radius = CreaturePerception.HearingRadius(
                    ObservedSpeed(p),
                    _def.QuietHearingRange, _def.WalkHearingRange, _def.SprintHearingRange,
                    _def.WalkSpeedThreshold, _def.SprintSpeedThreshold);
                if ((p.transform.position - transform.position).sqrMagnitude <= radius * radius)
                    Hear(p.transform.position);
            }

            if (memoryTimer > 0f)
            {
                memoryTimer -= dt;
                MoveTo(lastHeardPosition, _def.ChaseSpeed);
                // Mord tout ce qui est à portée pendant qu'il investigue (même sans « voir »).
                SetVisualState(TryMeleeAttack(NearestAlive(_def.AttackRange * 1.5f)) ? StateAggressive : StateAlert);
            }
            else
            {
                WanderAround(Home, _def.PatrolRadius, _def.PatrolSpeed);
                SetVisualState(StateIdle);
            }
        }
    }
}
