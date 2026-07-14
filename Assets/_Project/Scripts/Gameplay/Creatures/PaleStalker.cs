using UnityEngine;
using UnityEngine.AI;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Le Pâle (SPEC §4.8) : stalker des ruines. Approche quand on ne le regarde pas,
    /// se fige sous le regard (se retourner = contre-jeu, LC-Bracken-like), fuit la
    /// lumière directe de la lampe, attaque de dos. Traque en priorité les joueurs
    /// ISOLÉS (P4-8) ; repli sur le plus proche en ligne de vue si personne n'est isolé.
    /// </summary>
    public class PaleStalker : Creature
    {
        protected override void ServerTick(float dt)
        {
            PlayerHealth target = SelectIsolatedTarget(_def.SightRange, _def.IsolationRadius);
            if (target == null)
            {
                WanderAround(Home, _def.PatrolRadius, _def.PatrolSpeed);
                SetVisualState(StateIdle);
                return;
            }

            Transform pt = target.transform;

            // Lumière directe : la lampe répliquée est allumée et pointe vers lui → fuite.
            var lamp = target.GetComponent<HeadLamp>();
            if (lamp != null && lamp.IsOn
                && CreaturePerception.IsInCone(pt.position, pt.forward, transform.position, _def.LampConeDegrees, _def.LampFleeRange))
            {
                Vector3 away = transform.position + (transform.position - pt.position).normalized * _def.FleeDistance;
                if (NavMesh.SamplePosition(away, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                    MoveTo(hit.position, _def.FleeSpeed);
                SetVisualState(StateFleeing);
                return;
            }

            // Regardé : il se fige (l'orientation tête n'est pas répliquée — le corps suffit).
            if (CreaturePerception.IsInCone(pt.position, pt.forward, transform.position, _def.WatchConeDegrees, _def.SightRange))
            {
                StopMoving();
                SetVisualState(StateAlert);
                return;
            }

            // De dos : approche et attaque.
            if (!TryMeleeAttack(target))
                MoveTo(pt.position, _def.ChaseSpeed);
            SetVisualState(StateAggressive);
        }
    }
}
