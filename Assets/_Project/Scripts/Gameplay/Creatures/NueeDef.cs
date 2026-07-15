using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Tunables de la Nuée (SPEC §4.8) : essaim volant EXTÉRIEUR, sans NavMesh, piloté par
    /// steering aligné sur la gravité locale. Attirée par le bruit du laser de minage et par
    /// les lumières (lampes), harcèle le joueur ciblé. SO dédié (la Nuée ne rentre pas dans
    /// CreatureDef : pas de NavMesh, comportement de flocking + aggro à deux stimuli).
    /// Aucun nombre d'équilibrage en dur (règle absolue n°6) : tout vit ici.
    /// </summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Nuee", fileName = "SO_Creature_Nuee")]
    public class NueeDef : ScriptableObject
    {
        public string DisplayName = "Nuée";

        [Header("Déplacement (steering aligné gravité)")]
        [Tooltip("Vitesse de croisière du centre de l'essaim en errance (m/s).")]
        public float PatrolSpeed = 5f;
        [Tooltip("Vitesse du centre de l'essaim quand il fonce sur un stimulus/joueur (m/s).")]
        public float HuntSpeed = 11f;
        [Tooltip("Accélération max appliquée au centre par tick (m/s²), lisse les virages.")]
        public float MaxAcceleration = 14f;
        [Tooltip("Rayon d'errance autour du point d'ancrage quand inactif (m).")]
        public float PatrolRadius = 35f;
        [Tooltip("Distance d'arrivée sous laquelle un point d'errance est considéré atteint (m).")]
        public float ArriveRadius = 6f;

        [Header("Altitude de vol (au-dessus de la surface locale)")]
        [Tooltip("Altitude de croisière au-dessus du sol quand l'essaim erre (m).")]
        public float PatrolAltitude = 14f;
        [Tooltip("Altitude quand l'essaim harcèle un joueur (descend pour l'atteindre) (m).")]
        public float HarassAltitude = 3.5f;
        [Tooltip("Raideur (1/s) du rappel vers l'altitude cible (correction verticale douce).")]
        public float AltitudeStiffness = 1.5f;

        [Header("Aggro — bruit (événements de minage) et lumière (lampes allumées)")]
        [Tooltip("Portée d'audition des événements de bruit à loudness=1 (m), pondérée par l'intensité.")]
        public float NoiseHearingRange = 60f;
        [Tooltip("Force d'aggro (0-1) injectée par un ping de bruit perçu à loudness=1.")]
        public float NoiseStimulusStrength = 1f;
        [Tooltip("Portée de détection d'une lampe allumée (m). Le contre-jeu « leurre lumineux » passe par là.")]
        public float LightDetectionRange = 55f;
        [Tooltip("Force d'aggro (0-1) d'une lampe allumée à portée (source persistante tant qu'allumée).")]
        public float LightStimulusStrength = 0.8f;

        [Header("Aggro — dynamique (montée / décroissance / seuils)")]
        [Tooltip("Vitesse de montée de l'aggro vers la force du stimulus courant (par seconde).")]
        public float AggroBuildRate = 2.5f;
        [Tooltip("Vitesse de décroissance de l'aggro quand plus aucun stimulus (par seconde). Contre-jeu « sessions courtes ».")]
        public float AggroDecayRate = 0.25f;
        [Range(0f, 1f), Tooltip("Seuil d'aggro au-dessus duquel l'essaim s'engage (fonce sur la cible).")]
        public float EngageThreshold = 0.5f;
        [Range(0f, 1f), Tooltip("Seuil d'aggro sous lequel l'essaim se désengage (retour errance). < EngageThreshold pour l'hystérésis.")]
        public float DisengageThreshold = 0.2f;

        [Header("Harcèlement (dégâts de proximité, décidés serveur)")]
        [Tooltip("Rayon centre-essaim ↔ joueur sous lequel les dégâts s'appliquent (m). Couvre la hauteur de vol.")]
        public float DamageRadius = 5f;
        [Tooltip("Dégâts par morsure d'essaim.")]
        public float DamagePerHit = 6f;
        [Tooltip("Intervalle entre deux morsures (s).")]
        public float DamageInterval = 0.8f;

        [Header("Boids cosmétiques (simulés localement chez chaque client)")]
        [Tooltip("Nombre de boids affichés (purement visuel, jamais répliqué).")]
        public int BoidCount = 40;
        [Tooltip("Rayon du nuage de boids autour du centre quand calme (m).")]
        public float SpreadIdle = 9f;
        [Tooltip("Rayon du nuage quand agité/harcèle (l'essaim se resserre et s'agite) (m).")]
        public float SpreadAgitated = 4f;
        [Tooltip("Vitesse d'agitation des boids (m/s) — amplitude du bruit local.")]
        public float BoidJitterSpeed = 6f;
    }
}
