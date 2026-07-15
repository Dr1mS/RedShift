using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables de santé du joueur (SPEC §4.11).</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/PlayerHealth", fileName = "SO_Player_Health")]
    public class PlayerHealthDef : ScriptableObject
    {
        public float MaxHealth = 100f;

        [Header("Spectateur (caméra drone, SPEC §4.11)")]
        public float SpectatorSpeed = 8f;
        [Tooltip("Multiplicateur de vitesse avec Sprint maintenu.")]
        public float SpectatorFastMultiplier = 3f;
        public float SpectatorLookSensitivity = 0.12f;

        [Header("Spectateur — suivi coéquipier (3e personne, SPEC §4.11)")]
        [Tooltip("Distance derrière le coéquipier suivi (mètres).")]
        public float FollowDistance = 4f;
        [Tooltip("Hauteur au-dessus du coéquipier suivi, le long de SA gravité locale (mètres).")]
        public float FollowHeight = 2f;
        [Tooltip("Point visé au-dessus du corps suivi, le long de sa gravité (mètres) — cadre la tête, pas les pieds.")]
        public float FollowLookAtHeight = 1.4f;
        [Tooltip("Netteté du slerp d'orientation/up de la caméra de suivi (plus grand = plus réactif). Jamais de snap sur l'up (piège caméra du projet).")]
        public float FollowRotationSharpness = 10f;
        [Tooltip("Netteté du lissage de position quand la cible bouge (le passage d'une cible à l'autre, lui, snappe pour ne pas traverser la carte).")]
        public float FollowPositionSharpness = 12f;
    }
}
