using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Définition d'item : tout item a une masse et une valeur (SPEC §4.6).</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Item", fileName = "SO_Item_")]
    public class ItemDef : ScriptableObject
    {
        public string Id = "item";
        public string DisplayName = "Item";
        [Tooltip("Masse (kg) — impacte le port à 2 mains et la physique.")]
        public float Mass = 1f;
        [Tooltip("Valeur en crédits-équivalents pour le quota (SPEC §4.6).")]
        public int Value = 10;
        [Tooltip("Objet lourd : porté à 2 mains, bloque sprint et outils (SPEC §4.1). Géré en P3.")]
        public bool TwoHanded;
    }
}
