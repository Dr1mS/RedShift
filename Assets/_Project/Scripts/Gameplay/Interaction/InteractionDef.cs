using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables d'interaction joueur.</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Interaction", fileName = "SO_Player_Interaction")]
    public class InteractionDef : ScriptableObject
    {
        [Tooltip("Portée du raycast d'interaction (m).")]
        public float Range = 3f;
        [Tooltip("Vitesse de lancer au lâcher (m/s).")]
        public float TossSpeed = 3.5f;
    }
}
