using System;
using UnityEngine;

namespace Redshift.Core
{
    /// <summary>
    /// Event bus statique typé (SPEC §6.3) : découple gameplay → UI/audio/IA.
    /// Chez un pair, les événements sont levés une seule fois (le host déduplique
    /// ses passes serveur/client avant de lever). Les tests appellent ResetAll().
    /// </summary>
    public static class GameEvents
    {
        public static event Action<GamePhase> PhaseChanged;
        /// <summary>(valeur déposée, quota cible) du système courant.</summary>
        public static event Action<int, int> QuotaChanged;
        /// <summary>T-0 : l'étoile éclate, l'onde de choc part (SPEC §4.10).</summary>
        public static event Action SupernovaErupted;
        /// <summary>Bruit gameplay (position monde, intensité 0-1) : minage, chute… Écouté par les monstres (SPEC §4.5/§4.8).</summary>
        public static event Action<Vector3, float> NoiseEmitted;

        public static void RaisePhaseChanged(GamePhase phase) => PhaseChanged?.Invoke(phase);
        public static void RaiseQuotaChanged(int deposited, int target) => QuotaChanged?.Invoke(deposited, target);
        public static void RaiseSupernovaErupted() => SupernovaErupted?.Invoke();
        public static void RaiseNoiseEmitted(Vector3 position, float loudness) => NoiseEmitted?.Invoke(position, loudness);

        /// <summary>Purge tous les abonnés (tests, changement de scène dur).</summary>
        public static void ResetAll()
        {
            PhaseChanged = null;
            QuotaChanged = null;
            SupernovaErupted = null;
            NoiseEmitted = null;
        }
    }
}
