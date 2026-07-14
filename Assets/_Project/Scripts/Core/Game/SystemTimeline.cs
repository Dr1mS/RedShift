using System;

namespace Redshift.Core
{
    /// <summary>
    /// Timeline pure d'un système (SPEC §3.2) : Ftl → Arrival → Exploration → Critical →
    /// Collapse → Jump. Les transitions Arrival→…→Jump sont automatiques au timer ; le saut
    /// anticipé passe par <see cref="TryJumpNow"/> (fenêtre de saut = Collapse uniquement).
    /// À T-0 (fin du Collapse), le saut est automatique et la supernova éclate
    /// (<see cref="SupernovaErupted"/>). Aucune dépendance moteur : testable en EditMode.
    /// </summary>
    public class SystemTimeline
    {
        private readonly float arrivalDuration;
        private readonly float explorationDuration;
        private readonly float criticalDuration;
        private readonly float collapseDuration;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Ftl;
        public float PhaseElapsed { get; private set; }
        /// <summary>Vrai uniquement si le Collapse est allé à son terme (saut auto à T-0).</summary>
        public bool SupernovaErupted { get; private set; }

        public event Action<GamePhase> PhaseChanged;

        public SystemTimeline(float arrivalDuration, float explorationDuration, float criticalDuration, float collapseDuration)
        {
            this.arrivalDuration = Math.Max(0f, arrivalDuration);
            this.explorationDuration = Math.Max(0f, explorationDuration);
            this.criticalDuration = Math.Max(0f, criticalDuration);
            this.collapseDuration = Math.Max(0f, collapseDuration);
        }

        /// <summary>Durée d'une phase minutée ; +inf pour Ftl et Jump (non minutées).</summary>
        public float DurationOf(GamePhase phase) => phase switch
        {
            GamePhase.Arrival => arrivalDuration,
            GamePhase.Exploration => explorationDuration,
            GamePhase.Critical => criticalDuration,
            GamePhase.Collapse => collapseDuration,
            _ => float.PositiveInfinity,
        };

        public float PhaseRemaining => Math.Max(0f, DurationOf(CurrentPhase) - PhaseElapsed);

        /// <summary>Temps jusqu'à l'éruption (fin du Collapse). +inf en Ftl, 0 en Jump.</summary>
        public float TimeUntilSupernova => CurrentPhase switch
        {
            GamePhase.Ftl => float.PositiveInfinity,
            GamePhase.Arrival => PhaseRemaining + explorationDuration + criticalDuration + collapseDuration,
            GamePhase.Exploration => PhaseRemaining + criticalDuration + collapseDuration,
            GamePhase.Critical => PhaseRemaining + collapseDuration,
            GamePhase.Collapse => PhaseRemaining,
            _ => 0f,
        };

        /// <summary>
        /// Instabilité de l'étoile 0→1 : 0 jusqu'à la fin de l'Arrival, 1 à l'éruption.
        /// Pilote la teinte/échelle du StarDirector (la DA EST le timer, SPEC §7).
        /// </summary>
        public float Instability01
        {
            get
            {
                if (CurrentPhase is GamePhase.Ftl or GamePhase.Arrival)
                    return 0f;
                float span = explorationDuration + criticalDuration + collapseDuration;
                if (span <= 0f || CurrentPhase == GamePhase.Jump)
                    return 1f;
                return Math.Clamp(1f - TimeUntilSupernova / span, 0f, 1f);
            }
        }

        /// <summary>Sortie FTL : démarre (ou redémarre) le système en Arrival.</summary>
        public void StartSystem()
        {
            SupernovaErupted = false;
            PhaseElapsed = 0f;
            SetPhase(GamePhase.Arrival);
        }

        /// <summary>Avance la timeline ; gère plusieurs transitions dans un même pas.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return;

            PhaseElapsed += deltaSeconds;
            while (PhaseElapsed >= DurationOf(CurrentPhase))
            {
                PhaseElapsed -= DurationOf(CurrentPhase);
                GamePhase next = CurrentPhase + 1;
                if (next == GamePhase.Jump)
                    SupernovaErupted = true; // T-0 atteint : l'Arche saute, l'étoile éclate.
                SetPhase(next);
            }
        }

        /// <summary>Saut déclenché au cockpit — uniquement pendant la fenêtre de Collapse.</summary>
        public bool TryJumpNow()
        {
            if (CurrentPhase != GamePhase.Collapse)
                return false;
            PhaseElapsed = 0f;
            SetPhase(GamePhase.Jump);
            return true;
        }

        private void SetPhase(GamePhase phase)
        {
            if (CurrentPhase == phase)
                return;
            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
