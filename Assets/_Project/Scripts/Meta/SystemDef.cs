using UnityEngine;

namespace Redshift.Meta
{
    /// <summary>Configuration d'un système stellaire (SPEC §3.2) : timeline, quota, danger.</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/System", fileName = "SO_System_")]
    public class SystemDef : ScriptableObject
    {
        public string DisplayName = "Système";
        [Tooltip("Tier de danger/richesse (1 = départ) — pilote les tables de loot/monstres.")]
        public int Tier = 1;

        [Header("Timeline (secondes) — SPEC §3.2")]
        [Tooltip("Sortie FTL + scan système (étoile stable).")]
        public float ArrivalDuration = 60f;
        [Tooltip("Sorties navettes, minage, ruines (étoile stable → instable).")]
        public float ExplorationDuration = 1080f;
        [Tooltip("L'étoile gonfle, teinte rouge, événements d'instabilité en crescendo.")]
        public float CriticalDuration = 390f;
        [Tooltip("Compte à rebours de décollage : fenêtre de saut. À T-0 : supernova.")]
        public float CollapseDuration = 90f;

        [Header("Quota (SPEC §4.6 : quota(n) = base × growth^n)")]
        public int QuotaBase = 400;
        public float QuotaGrowth = 1.35f;
    }
}
