using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables de l'Arche-lite P3 (le vaisseau-mère praticable arrive en P5, SPEC §4.4).</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Ark", fileName = "SO_Ark_")]
    public class ArkDef : ScriptableObject
    {
        [Tooltip("Rayon (m) autour du cockpit dans lequel un joueur est « à bord » au saut.")]
        public float BoardingRadius = 12f;
    }
}
