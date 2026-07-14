using FishNet.Object;
using FishNet.Object.Synchronizing;
using Redshift.Core;
using Redshift.Meta;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Directeur de partie host-authoritative (SPEC §5, §6.3) : FSM GamePhase + timer +
    /// quota du système courant, répliqués. Le timer client est reconstruit depuis le tick
    /// réseau de début de phase (pas de SyncVar haute fréquence). Les changements sont
    /// diffusés localement via GameEvents (UI, StarDirector, IA).
    /// </summary>
    public class GameDirector : NetworkBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [SerializeField] private SystemDef _def;
        [SerializeField, Tooltip("Index du système dans l'expédition (quota croissant, 0 = premier).")]
        private int _systemIndex;

        private readonly SyncVar<GamePhase> _phase = new(GamePhase.Ftl);
        private readonly SyncVar<uint> _phaseStartTick = new();
        private readonly SyncVar<int> _quotaDeposited = new();
        private readonly SyncVar<int> _quotaTarget = new();

        // Serveur uniquement.
        private SystemTimeline timeline;
        private QuotaLedger ledger;
        private bool supernovaAnnounced;

        public SystemDef Def => _def;
        public GamePhase Phase => _phase.Value;
        public int QuotaDeposited => _quotaDeposited.Value;
        public int QuotaTarget => _quotaTarget.Value;
        public bool IsQuotaMet => _quotaDeposited.Value >= _quotaTarget.Value;

        /// <summary>Temps restant (s) dans la phase courante ; +inf pour Ftl/Jump.</summary>
        public float PhaseRemaining
        {
            get
            {
                float duration = DurationOf(_phase.Value);
                if (float.IsPositiveInfinity(duration))
                    return float.PositiveInfinity;
                return Mathf.Max(0f, duration - PhaseElapsed);
            }
        }

        /// <summary>Temps (s) jusqu'à l'éruption (fin du Collapse) ; +inf en Ftl, 0 en Jump.</summary>
        public float TimeUntilSupernova => _phase.Value switch
        {
            GamePhase.Ftl => float.PositiveInfinity,
            GamePhase.Arrival => PhaseRemaining + _def.ExplorationDuration + _def.CriticalDuration + _def.CollapseDuration,
            GamePhase.Exploration => PhaseRemaining + _def.CriticalDuration + _def.CollapseDuration,
            GamePhase.Critical => PhaseRemaining + _def.CollapseDuration,
            GamePhase.Collapse => PhaseRemaining,
            _ => 0f,
        };

        /// <summary>Instabilité 0→1 de l'étoile (StarDirector) : 0 jusqu'en fin d'Arrival, 1 à l'éruption.</summary>
        public float Instability01
        {
            get
            {
                if (_phase.Value is GamePhase.Ftl or GamePhase.Arrival)
                    return 0f;
                if (_phase.Value == GamePhase.Jump)
                    return 1f;
                float span = _def.ExplorationDuration + _def.CriticalDuration + _def.CollapseDuration;
                return span <= 0f ? 1f : Mathf.Clamp01(1f - TimeUntilSupernova / span);
            }
        }

        private float PhaseElapsed
            => (float)((TimeManager.Tick - _phaseStartTick.Value) * TimeManager.TickDelta);

        private void Awake()
        {
            Instance = this;
            _phase.OnChange += OnPhaseChanged;
            _quotaDeposited.OnChange += OnQuotaChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            timeline = new SystemTimeline(_def.ArrivalDuration, _def.ExplorationDuration, _def.CriticalDuration, _def.CollapseDuration);
            ledger = new QuotaLedger(QuotaRules.QuotaFor(_def.QuotaBase, _def.QuotaGrowth, _systemIndex));
            _quotaTarget.Value = ledger.Target;
            timeline.StartSystem();
            SyncPhase();
        }

        private void Update()
        {
            if (!IsServerInitialized || timeline == null)
                return;

            timeline.Tick(Time.deltaTime);
            if (timeline.CurrentPhase != _phase.Value)
                SyncPhase();

            if (timeline.SupernovaErupted && !supernovaAnnounced)
            {
                supernovaAnnounced = true;
                SupernovaEruptedRpc();
            }
        }

        /// <summary>Serveur : crédite la valeur d'un dépôt en soute (SPEC §4.6).</summary>
        [Server]
        public void DepositValue(int value)
        {
            ledger.Deposit(value);
            _quotaDeposited.Value = ledger.Deposited;
        }

        /// <summary>Serveur : saut déclenché au cockpit — fenêtre de Collapse uniquement.</summary>
        [Server]
        public bool TryJumpNow()
        {
            if (!timeline.TryJumpNow())
                return false;
            SyncPhase();
            return true;
        }

        [Server]
        private void SyncPhase()
        {
            _phase.Value = timeline.CurrentPhase;
            _phaseStartTick.Value = TimeManager.Tick;
        }

        [ObserversRpc]
        private void SupernovaEruptedRpc() => GameEvents.RaiseSupernovaErupted();

        private float DurationOf(GamePhase phase) => phase switch
        {
            GamePhase.Arrival => _def.ArrivalDuration,
            GamePhase.Exploration => _def.ExplorationDuration,
            GamePhase.Critical => _def.CriticalDuration,
            GamePhase.Collapse => _def.CollapseDuration,
            _ => float.PositiveInfinity,
        };

        private void OnPhaseChanged(GamePhase prev, GamePhase next, bool asServer)
        {
            // Le host reçoit les deux passes : ne lever l'événement qu'une fois.
            if (asServer && IsClientInitialized)
                return;
            GameEvents.RaisePhaseChanged(next);
        }

        private void OnQuotaChanged(int prev, int next, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return;
            GameEvents.RaiseQuotaChanged(next, _quotaTarget.Value);
        }
    }
}
