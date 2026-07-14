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
    }
}
