using UnityEngine;

namespace Redshift.Voice
{
    /// <summary>
    /// Tunables du chat vocal (SPEC §5 : voice positionnel ~20 m, canal radio, canal morts).
    ///
    /// <para>Règle projet n°6 : tout l'équilibrage (portées/distances) vit ici, jamais en dur
    /// dans le code. Les distances Vivox sont des <b>entiers</b> en « unités de distance »
    /// arbitraires — on cale l'échelle sur les mètres du jeu (1 unité = 1 m).</para>
    ///
    /// <para>Aucun type Vivox n'est référencé ici : le mapping vers <c>Channel3DProperties</c>
    /// se fait dans <see cref="VoiceService"/>, pour garder ce SO (et l'assembly de données)
    /// indépendant du package Vivox.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Voice", fileName = "SO_Voice_Default")]
    public class VoiceDef : ScriptableObject
    {
        [Header("Canal positionnel (SPEC §5 : proximité ~20 m)")]
        [Tooltip("Distance max (m) à laquelle un joueur est audible dans le canal positionnel. SPEC : ~20 m.")]
        public int AudibleDistanceMeters = 20;

        [Tooltip("Distance (m) sous laquelle la voix est à plein volume avant de commencer à s'atténuer. " +
                 "Vivox recommande ~la moitié de la hauteur d'un avatar (≈1 m). Doit être 0 ≤ x ≤ AudibleDistance.")]
        public int ConversationalDistanceMeters = 1;

        [Tooltip("Intensité de l'atténuation au-delà de la distance conversationnelle " +
                 "(1 = normal, >1 fade plus vite, <1 plus lent).")]
        public float AudioFadeIntensity = 1f;

        [Tooltip("Modèle d'atténuation. 0 = InverseByDistance (le plus réaliste, recommandé Vivox), " +
                 "1 = LinearByDistance, 2 = ExponentialByDistance.")]
        [Range(0, 2)]
        public int AudioFadeModel = 0;

        [Header("Noms de canaux (identifiants réseau — non-équilibrage)")]
        [Tooltip("Préfixe des canaux, suffixé par un identifiant de session pour isoler les runs. " +
                 "Le suffixe est fourni à l'exécution par VoiceService.")]
        public string PositionalChannelPrefix = "redshift-prox";
        public string DeadChannelPrefix = "redshift-dead";
        public string RadioChannelPrefix = "redshift-radio";
    }
}
