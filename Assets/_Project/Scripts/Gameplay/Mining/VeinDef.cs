using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Définition d'un filon de minerai (SPEC §4.5) : HP + table de rendement.</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Vein", fileName = "SO_Vein_")]
    public class VeinDef : ScriptableObject
    {
        public string DisplayName = "Filon";
        [Tooltip("Tier de minerai (difficulté du système, SPEC §4.5).")]
        public int Tier = 1;
        public float MaxHp = 100f;
        [Tooltip("Unités de minerai produites sur la vie du filon.")]
        public int OreUnits = 5;
        [Tooltip("Impulsion d'éjection des minerais à l'apparition (m/s).")]
        public float OreEjectSpeed = 2f;
    }
}
