using System.Collections.Generic;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Logique pure de routage vocal (SPEC §5 voice + §4.11 canal morts) — testable EditMode.
    ///
    /// <para>Contrainte de l'API Vivox qui façonne ce modèle : Vivox n'a PAS de drapeau
    /// « transmettre » par canal. La transmission est une politique GLOBALE unique
    /// (<c>TransmissionMode.None</c> / <c>Single:&lt;canal&gt;</c>) et « écouter un canal » = en être
    /// membre (on entend tous les canaux rejoints). Ce routeur émet donc exactement les deux
    /// grandeurs qui se mappent 1:1 sur l'API :</para>
    /// <list type="number">
    /// <item>l'ensemble d'appartenance voulu ⊆ { Positional, Dead, Radio } (canaux à rejoindre) ;</item>
    /// <item>la cible de transmission unique ∈ { None, Positional, Dead, Radio } (politique globale).</item>
    /// </list>
    ///
    /// <para>Invariant garanti : la cible de transmission est toujours incluse dans l'appartenance
    /// (on ne peut pas transmettre dans un canal qu'on n'a pas rejoint).</para>
    ///
    /// <para>Règles (produit croisé complet, cf. tests) :</para>
    /// <list type="bullet">
    /// <item><b>Vivant, radio OFF</b> → membre { Positional }, transmet Positional.</item>
    /// <item><b>Vivant, radio ON</b> (talkie, PTT) → membre { Positional, Radio }, transmet Radio
    /// (la voix part sur la radio longue portée ; on continue d'entendre le positionnel).</item>
    /// <item><b>Mort</b> → membre { Dead }, transmet Dead. Le mort QUITTE le positionnel :
    /// cela satisfait à la fois la garantie SPEC (pas de bleed mort→vivants, §4.11) et la lecture
    /// « canal morts séparé, simple » (les morts n'entendent pas les vivants). La radio est ignorée
    /// tant qu'on est mort (un cadavre ne tient pas de talkie).</item>
    /// </list>
    ///
    /// <para>DRAPEAU documenté — <see cref="DeadHearsLiving"/> : si Adrien veut que les morts
    /// entendent les vivants (mode « spectateur commente »), passer ce drapeau à vrai ajoute
    /// Positional à l'appartenance des morts SANS leur rendre la transmission (ils restent muets
    /// pour les vivants). C'est un changement d'une ligne, d'où l'intérêt de garder ce point
    /// explicite : la SPEC garantit uniquement l'absence de bleed mort→vivants ; « les morts
    /// n'entendent pas les vivants » est notre lecture de « séparé ».</para>
    /// </summary>
    public static class VoiceRouting
    {
        /// <summary>Les trois canaux logiques du jeu.</summary>
        public enum Channel
        {
            /// <summary>Aucun canal (cible de transmission = silence).</summary>
            None,
            /// <summary>Voix de proximité 3D (SPEC : portée ~20 m). Le canal des vivants.</summary>
            Positional,
            /// <summary>Canal des morts, non positionnel, séparé (SPEC §4.11).</summary>
            Dead,
            /// <summary>Canal radio d'équipe (talkie), non positionnel, longue portée (SPEC §5).</summary>
            Radio
        }

        /// <summary>
        /// Si vrai, les morts restent membres du canal positionnel (ils ENTENDENT les vivants),
        /// mais ne transmettent jamais vers eux. Faux par défaut = lecture SPEC « canal morts
        /// séparé, simple ». Voir la doc de la classe. <c>static readonly</c> (pas <c>const</c>) pour
        /// que basculer la valeur ne rende aucune branche « code mort » à la compilation.
        /// </summary>
        public static readonly bool DeadHearsLiving = false;

        /// <summary>
        /// Décision de routage : l'ensemble des canaux à rejoindre et le canal unique de transmission.
        /// </summary>
        public readonly struct Decision
        {
            /// <summary>Canaux dont le joueur local doit être membre (à rejoindre/quitter pour converger).</summary>
            public IReadOnlyList<Channel> Membership { get; }

            /// <summary>Canal unique de transmission (politique globale Vivox), ou <see cref="Channel.None"/>.</summary>
            public Channel Transmit { get; }

            public Decision(IReadOnlyList<Channel> membership, Channel transmit)
            {
                Membership = membership;
                Transmit = transmit;
            }

            /// <summary>Vrai si <paramref name="channel"/> fait partie de l'appartenance voulue.</summary>
            public bool IsMember(Channel channel)
            {
                for (int i = 0; i < Membership.Count; i++)
                    if (Membership[i] == channel)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Calcule la décision de routage à partir de l'état local du joueur.
        /// </summary>
        /// <param name="isDead">Le joueur local est-il mort (spectateur) ?</param>
        /// <param name="radioTransmit">La touche talkie (PTT) est-elle maintenue ? Ignoré si mort.</param>
        public static Decision Resolve(bool isDead, bool radioTransmit)
        {
            if (isDead)
            {
                // Mort : canal morts uniquement. La radio est ignorée (pas de talkie pour un cadavre).
                // Drapeau : si DeadHearsLiving, on ajoute le positionnel en écoute (jamais en transmission).
                if (DeadHearsLiving)
                    return new Decision(new[] { Channel.Dead, Channel.Positional }, Channel.Dead);
                return new Decision(new[] { Channel.Dead }, Channel.Dead);
            }

            if (radioTransmit)
            {
                // Vivant + PTT talkie : membre du positionnel (écoute) ET de la radio ; transmet sur la radio.
                return new Decision(new[] { Channel.Positional, Channel.Radio }, Channel.Radio);
            }

            // Vivant, radio au repos : canal positionnel seul, transmission positionnelle.
            return new Decision(new[] { Channel.Positional }, Channel.Positional);
        }

        /// <summary>
        /// Diff pur d'appartenance : canaux à REJOINDRE pour atteindre <paramref name="desired"/>
        /// depuis <paramref name="current"/>. Testable sans Vivox — c'est la moitié « join » de la
        /// réconciliation appliquée par le service.
        /// </summary>
        public static List<Channel> ChannelsToJoin(IReadOnlyList<Channel> current, IReadOnlyList<Channel> desired)
        {
            var result = new List<Channel>();
            if (desired == null)
                return result;
            for (int i = 0; i < desired.Count; i++)
            {
                Channel c = desired[i];
                if (c == Channel.None)
                    continue;
                if (!Contains(current, c) && !Contains(result, c))
                    result.Add(c);
            }
            return result;
        }

        /// <summary>
        /// Diff pur d'appartenance : canaux à QUITTER pour atteindre <paramref name="desired"/>
        /// depuis <paramref name="current"/>. Moitié « leave » de la réconciliation.
        /// </summary>
        public static List<Channel> ChannelsToLeave(IReadOnlyList<Channel> current, IReadOnlyList<Channel> desired)
        {
            var result = new List<Channel>();
            if (current == null)
                return result;
            for (int i = 0; i < current.Count; i++)
            {
                Channel c = current[i];
                if (c == Channel.None)
                    continue;
                if (!Contains(desired, c) && !Contains(result, c))
                    result.Add(c);
            }
            return result;
        }

        private static bool Contains(IReadOnlyList<Channel> list, Channel value)
        {
            if (list == null)
                return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == value)
                    return true;
            return false;
        }
    }
}
