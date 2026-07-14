using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Base des monstres (SPEC §4.8, §5) : simulation host-authoritative sur NavMesh
    /// (les poches sont y-up standard, D6), position répliquée par NetworkTransform
    /// server-auth. Les clients ne simulent rien (agent désactivé). L'état est teinté
    /// sur le visuel pour la lisibilité greybox (animations en P6).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class Creature : NetworkBehaviour
    {
        protected const byte StateIdle = 0;
        protected const byte StateAlert = 1;
        protected const byte StateAggressive = 2;
        protected const byte StateFleeing = 3;

        [SerializeField] protected CreatureDef _def;
        [SerializeField, Tooltip("Visuel teinté par état (lisibilité greybox).")]
        private Renderer _stateRenderer;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly SyncVar<byte> _visualState = new();

        protected NavMeshAgent agent;

        private float lastAttackTime = float.NegativeInfinity;
        private PlayerHealth[] players = System.Array.Empty<PlayerHealth>();
        private float playersRefreshTimer;
        private readonly Dictionary<PlayerHealth, Vector3> lastPositions = new();
        private readonly Dictionary<PlayerHealth, float> observedSpeeds = new();
        private Color baseColor = Color.white;
        private MaterialPropertyBlock block;
        private Vector3 home;

        protected Vector3 Home => home;

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            // Le NavMeshAgent natif tente de s'attacher au NavMesh dès la
            // désérialisation de la scène (avant même le premier Awake
            // scripté), donc avant que PocketNavMesh n'ait pu enregistrer la
            // NavMeshData de la poche (« Failed to create agent because
            // there is no valid NavMesh », reproductible en build standalone
            // — l'instance de scène porte désormais un override enabled=false
            // pour empêcher cette attache précoce). On réactive explicitement
            // en OnStartServer, qui s'exécute après tout le chargement de
            // scène et l'enregistrement du NavMesh de la poche ; défense en
            // profondeur ici au cas où l'override serait un jour perdu.
            agent.enabled = false;
            block = new MaterialPropertyBlock();
            if (_stateRenderer != null)
                baseColor = _stateRenderer.sharedMaterial.color;
            _visualState.OnChange += (_, next, _) => ApplyStateColor(next);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            home = transform.position;
            // La simulation vit chez le host uniquement : seul le serveur active l'agent.
            agent.enabled = true;
        }

        private void Update()
        {
            if (!IsServerInitialized)
                return;
            RefreshPlayers(Time.deltaTime);
            ServerTick(Time.deltaTime);
        }

        /// <summary>FSM de la créature, exécutée host-side chaque frame.</summary>
        protected abstract void ServerTick(float dt);

        private void RefreshPlayers(float dt)
        {
            playersRefreshTimer -= dt;
            if (playersRefreshTimer <= 0f)
            {
                playersRefreshTimer = 2f;
                players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
            }

            // Vitesse observée depuis la position répliquée : sert d'ouïe (pas d'état crouch réseau).
            foreach (PlayerHealth p in players)
            {
                if (p == null)
                    continue;
                Vector3 pos = p.transform.position;
                if (lastPositions.TryGetValue(p, out Vector3 last) && dt > 0f)
                {
                    float delta = (pos - last).magnitude;
                    // Un téléport (delta énorme sur une frame) ne doit pas compter comme du bruit.
                    // On saute la mise à jour de la vitesse observée mais on garde lastPositions
                    // à jour ci-dessous, sinon la frame suivante re-verrait le même saut.
                    if (CreaturePerception.IsContinuousMotion(delta))
                    {
                        float instantaneous = delta / dt;
                        observedSpeeds[p] = Mathf.Lerp(observedSpeeds.TryGetValue(p, out float s) ? s : 0f, instantaneous, 0.3f);
                    }
                }
                lastPositions[p] = pos;
            }
        }

        protected float ObservedSpeed(PlayerHealth player)
            => observedSpeeds.TryGetValue(player, out float s) ? s : 0f;

        protected IEnumerable<PlayerHealth> AlivePlayers()
        {
            foreach (PlayerHealth p in players)
                if (p != null && !p.IsDead && !p.IsEvacuated)
                    yield return p;
        }

        protected PlayerHealth NearestAlive(float maxRange = float.MaxValue, bool requireLineOfSight = false)
        {
            PlayerHealth best = null;
            float bestSqr = maxRange * maxRange;
            foreach (PlayerHealth p in AlivePlayers())
            {
                float sqr = (p.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr)
                    continue;
                if (requireLineOfSight && !HasLineOfSight(p))
                    continue;
                bestSqr = sqr;
                best = p;
            }
            return best;
        }

        // Réutilisées chaque tick sans réallouer (host-only, monothread).
        private readonly List<Vector3> _alivePositions = new();
        private readonly List<PlayerHealth> _aliveList = new();
        private readonly List<int> _candidateIndices = new();

        /// <summary>
        /// Cible « stalker » du Pâle (SPEC §4.8) : parmi les joueurs vivants EN LIGNE DE VUE et à
        /// portée (<paramref name="sightRange"/>), préfère un joueur isolé (aucun autre vivant à
        /// moins de <paramref name="isolationRadius"/>), sinon replie sur le plus proche.
        /// L'isolement se calcule contre TOUS les vivants, y compris hors ligne de vue de la
        /// créature (un coéquipier caché protège quand même son voisin). Renvoie null si aucun
        /// candidat visible. Logique pure dans <see cref="CreaturePerception.SelectStalkTarget"/>.
        /// </summary>
        protected PlayerHealth SelectIsolatedTarget(float sightRange, float isolationRadius)
        {
            _alivePositions.Clear();
            _aliveList.Clear();
            _candidateIndices.Clear();

            float sightSqr = sightRange * sightRange;
            foreach (PlayerHealth p in AlivePlayers())
            {
                int index = _aliveList.Count;
                _aliveList.Add(p);
                _alivePositions.Add(p.transform.position);

                if ((p.transform.position - transform.position).sqrMagnitude <= sightSqr && HasLineOfSight(p))
                    _candidateIndices.Add(index);
            }

            int chosen = CreaturePerception.SelectStalkTarget(_alivePositions, _candidateIndices, transform.position, isolationRadius);
            return chosen >= 0 ? _aliveList[chosen] : null;
        }

        /// <summary>Ligne de vue : bloquée par la géométrie (couches Player et Creature ignorées).</summary>
        protected bool HasLineOfSight(PlayerHealth player)
        {
            Vector3 eye = transform.position + transform.up * 1.5f;
            Vector3 head = player.transform.position + player.transform.up * 1.2f;
            int mask = ~((1 << 6) | (1 << 7)); // tout sauf Player et Creature
            return !Physics.Linecast(eye, head, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Vrai si la cible est à portée de mêlée ; applique les dégâts si le cooldown le permet.</summary>
        protected bool TryMeleeAttack(PlayerHealth target)
        {
            if (target == null || target.IsDead)
                return false;
            if (Vector3.Distance(transform.position, target.transform.position) > _def.AttackRange)
                return false;
            if (Time.time - lastAttackTime >= _def.AttackCooldown)
            {
                lastAttackTime = Time.time;
                target.ApplyDamage(_def.AttackDamage);
            }
            return true;
        }

        protected void MoveTo(Vector3 position, float speed)
        {
            agent.speed = speed;
            agent.SetDestination(position);
        }

        protected void StopMoving()
        {
            if (agent.hasPath)
                agent.ResetPath();
        }

        protected void WanderAround(Vector3 center, float radius, float speed)
        {
            agent.speed = speed;
            // On relance une errance quand l'agent est arrivé. Le seuil doit être ALIGNÉ sur
            // stoppingDistance : l'agent freine à vel=0 dès remainingDistance < stoppingDistance
            // (1.2 sur les prefabs). Un seuil de repath inférieur (l'ancien 0.8 codé en dur)
            // laissait l'agent figé dans la bande morte [0.8, 1.2] — arrivé pour l'agent, encore
            // en trajet pour nous → plus jamais de repath, créature immobile (P4-8).
            if (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.1f))
                return;
            Vector3 candidate = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        protected void SetVisualState(byte state)
        {
            if (IsServerInitialized && _visualState.Value != state)
                _visualState.Value = state;
        }

        private void ApplyStateColor(byte state)
        {
            if (_stateRenderer == null)
                return;
            Color tint = state switch
            {
                StateAlert => Color.Lerp(baseColor, new Color(1f, 0.6f, 0.1f), 0.6f),
                StateAggressive => Color.Lerp(baseColor, Color.red, 0.65f),
                StateFleeing => Color.Lerp(baseColor, new Color(0.3f, 0.5f, 1f), 0.6f),
                _ => baseColor,
            };
            block.SetColor(BaseColorId, tint);
            _stateRenderer.SetPropertyBlock(block);
        }
    }
}
