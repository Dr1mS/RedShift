using System.Collections.Generic;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Logique pure de sélection de cible du spectateur (SPEC §4.11) — testable EditMode.
    /// Le cycle opère sur des <b>identités stables</b> (ClientId trié croissant), jamais sur
    /// un index dans une liste volatile : <c>FindObjectsByType</c> ne garantit aucun ordre et
    /// cet ordre change à chaque déconnexion/spawn. En raisonnant sur le ClientId, le chemin
    /// testé est exactement le chemin runtime.
    /// </summary>
    public static class SpectatorTargeting
    {
        /// <summary>Valeur sentinelle « aucune cible » (repli caméra libre).</summary>
        public const int NoTarget = -1;

        /// <summary>
        /// ClientId de la cible suivante à suivre, en partant de <paramref name="currentClientId"/>
        /// et en avançant de +1 (forward) ou -1 (arrière) dans la liste des candidats <b>triée
        /// croissante par ClientId</b>. Retourne <see cref="NoTarget"/> si aucun candidat.
        ///
        /// <para>Règles :</para>
        /// <list type="bullet">
        /// <item>Liste vide → <see cref="NoTarget"/> (retombe en caméra libre).</item>
        /// <item><paramref name="currentClientId"/> absent (mort, déconnexion, ou libre) →
        /// premier candidat (forward) ou dernier (arrière) : reprise propre après disparition
        /// de la cible ou depuis la caméra libre.</item>
        /// <item>Présent → le voisin cyclique dans la direction demandée (wrap aux extrémités).</item>
        /// </list>
        /// </summary>
        /// <param name="sortedCandidateClientIds">ClientId des cibles valides, triés croissants (sans doublon).</param>
        /// <param name="currentClientId">ClientId suivi actuellement, ou <see cref="NoTarget"/> en caméra libre.</param>
        /// <param name="forward">Vrai pour la cible suivante, faux pour la précédente.</param>
        public static int NextClientId(IReadOnlyList<int> sortedCandidateClientIds, int currentClientId, bool forward)
        {
            if (sortedCandidateClientIds == null || sortedCandidateClientIds.Count == 0)
                return NoTarget;

            int count = sortedCandidateClientIds.Count;
            int currentIndex = IndexOf(sortedCandidateClientIds, currentClientId);

            if (currentIndex < 0)
            {
                // Cible absente (disparue) ou caméra libre : entrer par une extrémité.
                return forward ? sortedCandidateClientIds[0] : sortedCandidateClientIds[count - 1];
            }

            int step = forward ? 1 : -1;
            int nextIndex = ((currentIndex + step) % count + count) % count;
            return sortedCandidateClientIds[nextIndex];
        }

        /// <summary>
        /// Vrai si <paramref name="clientId"/> est toujours une cible valide (présent dans la
        /// liste triée courante). Utilisé chaque frame pour détecter la disparition de la cible
        /// suivie (mort, évacuation, déconnexion) et déclencher un repli.
        /// </summary>
        public static bool IsStillValid(IReadOnlyList<int> sortedCandidateClientIds, int clientId)
            => clientId != NoTarget && IndexOf(sortedCandidateClientIds, clientId) >= 0;

        private static int IndexOf(IReadOnlyList<int> list, int value)
        {
            if (list == null)
                return -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == value)
                    return i;
            return -1;
        }
    }
}
