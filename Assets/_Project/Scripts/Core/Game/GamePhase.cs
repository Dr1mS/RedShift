namespace Redshift.Core
{
    /// <summary>Phases d'un système stellaire (SPEC §3.2, §6.3). Pilotées host-side, répliquées.</summary>
    public enum GamePhase
    {
        Ftl = 0,
        Arrival = 1,
        Exploration = 2,
        Critical = 3,
        Collapse = 4,
        Jump = 5,
    }
}
