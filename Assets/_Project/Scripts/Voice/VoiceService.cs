using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FishNet;
using FishNet.Managing;
using Redshift.Gameplay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace Redshift.Voice
{
    /// <summary>
    /// Chat vocal Vivox (P4-13, SPEC §5 + §4.11) — MonoBehaviour NON-networké, GO dédié en scène,
    /// calqué sur le pattern de SteamLobbyService : états + event <see cref="StateChanged"/>, init
    /// gardée à la demande, jamais d'exception qui remonte.
    ///
    /// <para><b>Contrainte connue</b> : le projet n'est PAS lié à Unity Cloud
    /// (<c>Application.cloudProjectId</c> vide). Vivox (UGS) ne peut donc ni s'initialiser ni se
    /// connecter aujourd'hui. Le chemin « voice indisponible » est donc le chemin NOMINAL :
    /// on détecte l'absence de <c>cloudProjectId</c> AVANT tout appel UGS, on passe en
    /// <see cref="VoiceState.Unavailable"/>, on loggue UNE fois, et le jeu tourne normalement.
    /// Le jour où Adrien lie le projet + active Vivox au dashboard, ce même code enchaîne
    /// init → login → canaux sans retouche.</para>
    ///
    /// <para><b>Réseau</b> : ce service n'ajoute AUCUN composant réseau. Il observe localement,
    /// comme le HUD : bind du <see cref="PlayerHealth"/> propriétaire, poll de <c>IsDead</c>, et
    /// rejoint le canal positionnel une fois la connexion FishNet établie (client ou host).</para>
    ///
    /// <para><b>Routage</b> : toute la décision « quels canaux, quelle transmission » vit dans la
    /// classe pure <see cref="VoiceRouting"/> (testée en EditMode). Ce service ne fait qu'appliquer
    /// la décision à Vivox : réconciliation de l'appartenance (join/leave) + politique de
    /// transmission globale (Vivox n'a pas de flag TX par canal).</para>
    /// </summary>
    public class VoiceService : MonoBehaviour
    {
        /// <summary>Message loggué UNE fois quand le projet Cloud n'est pas lié (critère DoD central).</summary>
        private const string UnavailableLog =
            "[VoiceService] Projet Unity Cloud non lié — voice désactivé (action : lier le projet + activer Vivox)";

        public enum VoiceState
        {
            /// <summary>Voice impossible (projet Cloud non lié, ou échec d'init). État de repos actuel.</summary>
            Unavailable,
            /// <summary>Init UGS/Vivox + login en cours.</summary>
            Initializing,
            /// <summary>Connecté à Vivox, pas (encore) dans le canal positionnel.</summary>
            LoggedIn,
            /// <summary>Dans le canal positionnel (vivant) — état nominal en jeu quand la voix marche.</summary>
            InPositionalChannel,
            /// <summary>Échec explicite (exception attrapée) — jamais de crash, juste cet état + log.</summary>
            Failed
        }

        [Header("Données (équilibrage : portées) — règle projet n°6")]
        [SerializeField, Tooltip("SO des portées voice (positionnel ~20 m) + noms de canaux.")]
        private VoiceDef _def;

        [Header("Cadence technique (pas de l'équilibrage)")]
        [SerializeField, Tooltip("Intervalle (s) entre deux mises à jour de la position 3D Vivox depuis la tête du joueur local.")]
        private float _positionUpdateInterval = 0.2f;

        [SerializeField, Tooltip("Nom d'affichage Vivox par défaut si aucun n'est fourni (débogage).")]
        private string _defaultDisplayName = "Player";

        /// <summary>État courant (lu par le HUD).</summary>
        public VoiceState State { get; private set; } = VoiceState.Unavailable;

        /// <summary>Notifié à chaque changement d'état (le HUD se rafraîchit).</summary>
        public event Action StateChanged;

        /// <summary>Le joueur local est-il actuellement dans le canal des morts ? (lu par le HUD).</summary>
        public bool InDeadChannel { get; private set; }

        // --- État interne Vivox ---
        private bool _initEvaluated; // la garde d'init a-t-elle été évaluée au moins une fois ?
        private bool _unavailableLogged;
        private bool _initStarted;
        private bool _loggedIn;

        // Suffixe de session partagé par tous les joueurs d'un run (host-based : un lobby = un run).
        // Fixe pour l'instant ; suffisant tant qu'une seule session tourne à la fois par app Vivox.
        private const string SessionSuffix = "main";
        private string _positionalChannel;
        private string _deadChannel;
        private string _radioChannel;

        // Appartenance courante réellement rejointe côté Vivox (source de vérité pour la réconciliation).
        private readonly List<VoiceRouting.Channel> _currentMembership = new();
        private VoiceRouting.Channel _currentTransmit = VoiceRouting.Channel.None;
        private bool _reconciling;

        // --- Observation locale (comme le HUD) ---
        private PlayerHealth _localHealth;
        private Transform _localHead;
        private float _bindTimer;
        private bool _lastDead;
        private bool _radioTransmit; // piloté par le stub talkie SetRadioTransmit

        // --- Connexion réseau ---
        private NetworkManager _nm;
        private bool _joinedOnce;
        private float _positionTimer;

        private void Awake()
        {
            _positionalChannel = $"{Prefix(_def?.PositionalChannelPrefix, "redshift-prox")}-{SessionSuffix}";
            _deadChannel = $"{Prefix(_def?.DeadChannelPrefix, "redshift-dead")}-{SessionSuffix}";
            _radioChannel = $"{Prefix(_def?.RadioChannelPrefix, "redshift-radio")}-{SessionSuffix}";
        }

        private static string Prefix(string value, string fallback)
            => string.IsNullOrEmpty(value) ? fallback : value;

        private void Update()
        {
            // Première évaluation (garde cloudProjectId + log unique) : DOIT tourner une fois même si
            // l'état de repos est déjà Unavailable, sinon le log de diagnostic ne partirait jamais.
            if (!_initEvaluated)
            {
                _initEvaluated = true;
                EnsureInitialized();
            }

            // Voice définitivement indisponible : plus rien à tenter (log déjà émis une fois).
            if (State == VoiceState.Unavailable || State == VoiceState.Failed)
                return;

            TryBindLocalPlayer();
            TryJoinOnNetworkReady();
            PollDeathTransition();
            Update3DPosition();
        }

        // ---- API publique ---------------------------------------------------------------

        /// <summary>
        /// Démarre l'init Vivox à la demande, gardée. Si le projet Cloud n'est pas lié
        /// (<c>cloudProjectId</c> vide), passe en <see cref="VoiceState.Unavailable"/>, loggue UNE
        /// fois, et ne lève JAMAIS d'exception. Idempotent (ne relance pas si déjà en cours).
        /// Appelée automatiquement au premier besoin (connexion réseau prête).
        /// </summary>
        public async void EnsureInitialized()
        {
            if (_initStarted || _loggedIn)
                return;

            // GARDE CENTRALE : aucun appel UGS/Vivox si le projet n'est pas lié.
            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                if (!_unavailableLogged)
                {
                    Debug.Log(UnavailableLog);
                    _unavailableLogged = true;
                }
                SetState(VoiceState.Unavailable);
                return;
            }

            _initStarted = true;
            SetState(VoiceState.Initializing);

            try
            {
                // 1) UGS core : c'est ici que l'absence de cloudProjectId ferait échouer — d'où la garde ci-dessus.
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                // 2) Auth anonyme UGS : Vivox exige un joueur UGS connecté pour son token provider par défaut.
                await EnsureSignedInAsync();

                // 3) Vivox : init + login.
                if (VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized)
                    await VivoxService.Instance.InitializeAsync();

                var loginOptions = new LoginOptions { DisplayName = _defaultDisplayName };
                await VivoxService.Instance.LoginAsync(loginOptions);

                _loggedIn = true;
                SetState(VoiceState.LoggedIn);
            }
            catch (Exception e)
            {
                // Jamais de crash : on retombe en échec propre, loggué une fois.
                Debug.LogWarning($"[VoiceService] Init/login Vivox impossible : {e.Message}. Voice désactivé.");
                _initStarted = false;
                SetState(VoiceState.Failed);
            }
        }

        /// <summary>
        /// STUB TALKIE (SPEC §5 : canal radio d'équipe via item « talkie »). Pilote la transmission
        /// radio (PTT). L'ITEM talkie n'est PAS implémenté ici : cette API est branchée sur l'item
        /// en P5/P6 selon la SPEC (équip → hasRadio, gâchette → SetRadioTransmit). Sans item, seule
        /// cette méthode peut activer la radio. Zéro UI.
        /// </summary>
        /// <param name="transmit">Vrai tant que la gâchette talkie est maintenue.</param>
        public void SetRadioTransmit(bool transmit)
        {
            if (_radioTransmit == transmit)
                return;
            _radioTransmit = transmit;
            // Reconverge l'appartenance/transmission (rejoint/quitte le canal radio, bascule la TX).
            ReconcileRouting();
        }

        // ---- Auth UGS -------------------------------------------------------------------

        /// <summary>
        /// Connexion anonyme UGS si nécessaire : Vivox exige un joueur UGS authentifié pour générer
        /// son token d'accès (tokenKey volontairement absent du client — flux UGS). Le package
        /// com.unity.services.authentication est une dépendance validée du projet (2026-07-15).
        /// </summary>
        private static async Task EnsureSignedInAsync()
        {
            if (AuthenticationService.Instance.IsSignedIn)
                return;
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // ---- Connexion réseau -----------------------------------------------------------

        /// <summary>
        /// Rejoint le canal positionnel une fois la connexion FishNet établie (host ou client), une
        /// seule fois. Déclenche aussi l'init Vivox si pas encore faite.
        /// </summary>
        private void TryJoinOnNetworkReady()
        {
            if (_joinedOnce)
                return;

            _nm ??= InstanceFinder.NetworkManager;
            bool clientReady = _nm != null && _nm.ClientManager != null && _nm.ClientManager.Started;
            bool serverReady = _nm != null && _nm.ServerManager != null && _nm.ServerManager.Started;
            if (!clientReady && !serverReady)
                return;

            EnsureInitialized();

            if (!_loggedIn)
                return; // Pas encore connecté à Vivox (ou indisponible) : on réessaiera aux frames suivantes.

            _joinedOnce = true;
            ReconcileRouting(); // rejoint le positionnel (vivant, radio OFF au démarrage).
        }

        // ---- Observation locale (mort / tête) -------------------------------------------

        private void TryBindLocalPlayer()
        {
            if (_localHealth != null)
                return;
            _bindTimer -= Time.deltaTime;
            if (_bindTimer > 0f)
                return;
            _bindTimer = 0.5f;

            foreach (PlayerHealth candidate in FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude))
            {
                if (candidate == null || !candidate.IsOwner)
                    continue;
                _localHealth = candidate;
                // La tête (PlayerLook) porte la caméra locale : c'est la source de position 3D Vivox.
                PlayerLook look = candidate.GetComponentInChildren<PlayerLook>(true);
                _localHead = look != null ? look.transform : candidate.transform;
                _lastDead = candidate.IsDead;
                return;
            }
        }

        /// <summary>
        /// Détecte la transition vivant→mort (et le retour mort→vivant, système futur) par poll de
        /// <c>PlayerHealth.IsDead</c> — le SyncVar est privé, pas d'event public à hooker, on observe
        /// localement comme le HUD. À la mort : bascule sur le canal morts (séparé). Au retour :
        /// quitte le canal morts et revient au positionnel.
        /// </summary>
        private void PollDeathTransition()
        {
            if (_localHealth == null || !_joinedOnce)
                return;
            bool dead = _localHealth.IsDead;
            if (dead == _lastDead)
                return;
            _lastDead = dead;
            ReconcileRouting();
        }

        // ---- Position 3D Vivox ----------------------------------------------------------

        /// <summary>
        /// Pousse la position de la tête du joueur local dans le canal positionnel, à cadence
        /// <see cref="_positionUpdateInterval"/>. Sans effet si mort (plus dans le positionnel) ou
        /// si Vivox indisponible.
        /// </summary>
        private void Update3DPosition()
        {
            if (!_loggedIn || _localHead == null)
                return;
            if (!_currentMembership.Contains(VoiceRouting.Channel.Positional))
                return;

            _positionTimer -= Time.deltaTime;
            if (_positionTimer > 0f)
                return;
            _positionTimer = Mathf.Max(0.02f, _positionUpdateInterval);

            try
            {
                VivoxService.Instance.Set3DPosition(_localHead.gameObject, _positionalChannel);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoiceService] Set3DPosition impossible : {e.Message}");
            }
        }

        // ---- Réconciliation appartenance/transmission -----------------------------------

        /// <summary>
        /// Calcule la décision voulue (<see cref="VoiceRouting.Resolve"/>) depuis l'état local
        /// (mort ? radio ?) et converge l'appartenance Vivox : quitte les canaux en trop, rejoint
        /// les manquants, puis pose la transmission globale. Tout est try/catch : jamais de crash.
        /// </summary>
        private async void ReconcileRouting()
        {
            if (!_loggedIn || _reconciling)
                return;
            _reconciling = true;

            try
            {
                bool dead = _localHealth != null && _localHealth.IsDead;
                VoiceRouting.Decision decision = VoiceRouting.Resolve(dead, _radioTransmit);

                List<VoiceRouting.Channel> toLeave = VoiceRouting.ChannelsToLeave(_currentMembership, decision.Membership);
                List<VoiceRouting.Channel> toJoin = VoiceRouting.ChannelsToJoin(_currentMembership, decision.Membership);

                foreach (VoiceRouting.Channel c in toLeave)
                    await LeaveChannelAsync(c);

                foreach (VoiceRouting.Channel c in toJoin)
                    await JoinChannelAsync(c);

                await SetTransmitAsync(decision.Transmit);

                InDeadChannel = _currentMembership.Contains(VoiceRouting.Channel.Dead);
                SetState(_currentMembership.Contains(VoiceRouting.Channel.Positional)
                    ? VoiceState.InPositionalChannel
                    : VoiceState.LoggedIn);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoiceService] Réconciliation des canaux impossible : {e.Message}");
            }
            finally
            {
                _reconciling = false;
            }
        }

        private async Task JoinChannelAsync(VoiceRouting.Channel channel)
        {
            switch (channel)
            {
                case VoiceRouting.Channel.Positional:
                    // L'enum Vivox AudioFadeModel commence à 1 (InverseByDistance=1) : mapping
                    // explicite depuis la convention 0/1/2 du SO, jamais de cast direct.
                    AudioFadeModel fadeModel = (_def != null ? Mathf.Clamp(_def.AudioFadeModel, 0, 2) : 0) switch
                    {
                        1 => AudioFadeModel.LinearByDistance,
                        2 => AudioFadeModel.ExponentialByDistance,
                        _ => AudioFadeModel.InverseByDistance,
                    };
                    var props = new Channel3DProperties(
                        _def != null ? Mathf.Max(1, _def.AudibleDistanceMeters) : 20,
                        _def != null ? Mathf.Clamp(_def.ConversationalDistanceMeters, 0, Mathf.Max(1, _def.AudibleDistanceMeters)) : 1,
                        _def != null ? Mathf.Max(0f, _def.AudioFadeIntensity) : 1f,
                        fadeModel);
                    await VivoxService.Instance.JoinPositionalChannelAsync(_positionalChannel, ChatCapability.AudioOnly, props);
                    break;
                case VoiceRouting.Channel.Dead:
                    await VivoxService.Instance.JoinGroupChannelAsync(_deadChannel, ChatCapability.AudioOnly);
                    break;
                case VoiceRouting.Channel.Radio:
                    await VivoxService.Instance.JoinGroupChannelAsync(_radioChannel, ChatCapability.AudioOnly);
                    break;
                default:
                    return;
            }
            if (!_currentMembership.Contains(channel))
                _currentMembership.Add(channel);
        }

        private async Task LeaveChannelAsync(VoiceRouting.Channel channel)
        {
            string name = ChannelName(channel);
            if (name == null)
                return;
            await VivoxService.Instance.LeaveChannelAsync(name);
            _currentMembership.Remove(channel);
        }

        private async Task SetTransmitAsync(VoiceRouting.Channel transmit)
        {
            if (transmit == _currentTransmit)
                return;
            if (transmit == VoiceRouting.Channel.None)
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None);
            else
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, ChannelName(transmit));
            _currentTransmit = transmit;
        }

        private string ChannelName(VoiceRouting.Channel channel) => channel switch
        {
            VoiceRouting.Channel.Positional => _positionalChannel,
            VoiceRouting.Channel.Dead => _deadChannel,
            VoiceRouting.Channel.Radio => _radioChannel,
            _ => null
        };

        // ---- Interne --------------------------------------------------------------------

        private void SetState(VoiceState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke();
        }

        private async void OnDestroy()
        {
            if (!_loggedIn)
                return;
            try
            {
                await VivoxService.Instance.LogoutAsync();
            }
            catch { /* best effort à la fermeture */ }
        }
    }
}
