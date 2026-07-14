using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables d'un monstre (SPEC §4.8). Un asset par créature (SO_Creature_*).</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Creature", fileName = "SO_Creature_")]
    public class CreatureDef : ScriptableObject
    {
        public string DisplayName = "Créature";

        [Header("Déplacement")]
        public float PatrolSpeed = 1.8f;
        public float ChaseSpeed = 4.2f;
        public float FleeSpeed = 5f;
        [Tooltip("Rayon d'errance autour du point d'ancrage.")]
        public float PatrolRadius = 12f;

        [Header("Mêlée")]
        public float AttackRange = 1.9f;
        public float AttackDamage = 25f;
        public float AttackCooldown = 1.2f;

        [Header("Ouïe (Fouisseur — aveugle, SPEC : réagit au bruit)")]
        [Tooltip("Portée d'audition d'un joueur silencieux/accroupi (m).")]
        public float QuietHearingRange = 2.5f;
        [Tooltip("Portée d'audition d'un joueur qui marche (m).")]
        public float WalkHearingRange = 10f;
        [Tooltip("Portée d'audition d'un joueur qui sprinte (m).")]
        public float SprintHearingRange = 20f;
        [Tooltip("Vitesse (m/s) au-delà de laquelle un joueur « marche » (accroupi < seuil).")]
        public float WalkSpeedThreshold = 3f;
        [Tooltip("Vitesse (m/s) au-delà de laquelle un joueur « sprinte ».")]
        public float SprintSpeedThreshold = 6f;
        [Tooltip("Portée d'audition des événements de bruit (minage…), pondérée par l'intensité.")]
        public float NoiseHearingRange = 35f;
        [Tooltip("Durée (s) de poursuite du dernier bruit entendu.")]
        public float MemorySeconds = 6f;

        [Header("Vue (Pâle — stalker, SPEC : fuit la lumière, attaque de dos)")]
        public float SightRange = 18f;
        [Tooltip("Rayon d'isolement (m) : un joueur est « isolé » si aucun autre joueur vivant n'est à moins de cette distance. Le Pâle traque en priorité les isolés (SPEC §4.8).")]
        public float IsolationRadius = 20f;
        [Tooltip("Demi-angle (deg) du cône de regard joueur qui fige la créature.")]
        public float WatchConeDegrees = 55f;
        [Tooltip("Demi-angle (deg) du cône de lampe qui la fait fuir.")]
        public float LampConeDegrees = 35f;
        [Tooltip("Portée (m) de la lumière directe qui la fait fuir.")]
        public float LampFleeRange = 14f;
        [Tooltip("Distance (m) du point de fuite.")]
        public float FleeDistance = 9f;
    }
}
