using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables de la pioche laser (SPEC §4.5) : faisceau, chaleur, aggro sonore.</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/MiningLaser", fileName = "SO_Tool_MiningLaser_")]
    public class MiningLaserDef : ScriptableObject
    {
        [Header("Faisceau")]
        public float Range = 6f;
        public float DamagePerSecond = 20f;
        [Tooltip("Intervalle (s) d'envoi des dégâts accumulés au serveur.")]
        public float DamageSendInterval = 0.25f;

        [Header("Chaleur (surchauffe = pause forcée)")]
        [Tooltip("Temps de tir continu avant surchauffe (s).")]
        public float SecondsToOverheat = 6f;
        [Tooltip("Temps de refroidissement complet (s).")]
        public float SecondsToCool = 4f;
        [Range(0f, 0.99f), Tooltip("Chaleur sous laquelle le laser se réarme après surchauffe.")]
        public float ReArmThreshold = 0.35f;

        [Header("Aggro sonore (stub P3 — la Nuée arrive en P4)")]
        [Range(0f, 1f), Tooltip("Intensité du bruit émis pendant le minage.")]
        public float NoiseLoudness = 0.8f;
    }
}
