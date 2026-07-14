using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables de santé du joueur (SPEC §4.11).</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/PlayerHealth", fileName = "SO_Player_Health")]
    public class PlayerHealthDef : ScriptableObject
    {
        public float MaxHealth = 100f;
    }
}
