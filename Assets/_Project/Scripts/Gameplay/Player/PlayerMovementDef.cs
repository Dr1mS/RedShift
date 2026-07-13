using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables de déplacement joueur (règle absolue n°6 : aucun nombre en dur).</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Player Movement", fileName = "SO_Player_Movement")]
    public class PlayerMovementDef : ScriptableObject
    {
        [Header("Vitesses (m/s)")]
        public float WalkSpeed = 4.5f;
        public float SprintSpeed = 7.5f;
        public float CrouchSpeed = 2.2f;

        [Header("Accélérations (m/s²)")]
        public float GroundAcceleration = 40f;
        public float AirAcceleration = 8f;

        [Header("Saut / sol")]
        [Tooltip("Hauteur de saut (m) — convertie en vitesse selon la gravité locale.")]
        public float JumpHeight = 1.1f;
        [Tooltip("Distance de détection du sol sous la capsule (m).")]
        public float GroundCheckDistance = 0.25f;

        [Header("Alignement gravité")]
        [Tooltip("Vitesse du slerp d'alignement de l'up local (1/s). Jamais de snap (piège connu).")]
        public float UpAlignmentSharpness = 8f;

        [Header("Caméra")]
        public float LookSensitivity = 0.12f;
        public float PitchMin = -85f;
        public float PitchMax = 85f;

        [Header("Stamina (sprint)")]
        public float StaminaMax = 6f;
        public float StaminaDrainPerSecond = 1f;
        public float StaminaRegenPerSecond = 0.7f;
        [Tooltip("Délai (s) après le sprint avant régénération.")]
        public float StaminaRegenDelay = 1f;
        [Tooltip("Fraction de stamina minimale pour ré-autoriser le sprint après épuisement.")]
        [Range(0f, 1f)] public float SprintRecoveryThreshold = 0.25f;

        [Header("Capsule / accroupi")]
        public float StandingHeight = 1.8f;
        public float CrouchHeight = 1.1f;
        [Tooltip("Hauteur locale des yeux, debout (m).")]
        public float HeadHeight = 1.65f;
        [Tooltip("Vitesse d'interpolation debout/accroupi (1/s).")]
        public float CrouchLerpSharpness = 12f;
    }
}
