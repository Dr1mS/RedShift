using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables des portails de poche (SPEC D6, §6.3).</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Portal", fileName = "SO_Portal_")]
    public class PortalDef : ScriptableObject
    {
        [Tooltip("Anti aller-retour : délai (s) avant de pouvoir refranchir un portail.")]
        public float Cooldown = 0.75f;
    }
}
