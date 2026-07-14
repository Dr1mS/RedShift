using System;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Redshift.Networking
{
    /// <summary>
    /// Lobby Steam (P4-3) : héberger (amis-only, code = LobbyId), inviter via l'overlay,
    /// rejoindre par code ou par invitation Steam, puis basculer le transport FishNet sur
    /// FishyFacepunch et démarrer host/client.
    ///
    /// MonoBehaviour non-networké, GO dédié dans la scène. Pilote <see cref="NetworkBootstrap"/>
    /// (méthodes publiques <c>StartHost</c>/<c>StartClientTo</c>) plutôt que de dupliquer la
    /// logique de swap de transport : le transport n'est jamais hot-swappé, le NM est activé
    /// avec le bon transport au moment du démarrage (cf. commentaire de NetworkBootstrap).
    ///
    /// SteamClient.Init : appelé ici À LA DEMANDE (au premier host/join Steam), gardé par
    /// <c>!SteamClient.IsValid</c>, avec <c>asyncCallbacks:true</c> — même second argument que
    /// l'Init du transport FishyFacepunch (qui devient alors un no-op). asyncCallbacks:true
    /// fait que Facepunch pompe ses callbacks tout seul (pas de RunCallbacks() manuel), donc
    /// CreateLobbyAsync / OnGameLobbyJoinRequested / OnLobbyEntered fonctionnent sans boucle
    /// explicite. On n'appelle JAMAIS SteamClient.Shutdown (le transport ne coupe que ses
    /// sockets ; Shutdown couperait Steam sous les pieds du transport).
    ///
    /// Clé de lobby :
    ///   HostSteamId = SteamID64 de l'hôte (le client s'y connecte via SetClientAddress).
    /// </summary>
    public class SteamLobbyService : MonoBehaviour
    {
        /// <summary>Nombre max de joueurs (constante réseau, cf. SPEC §5 : 1-4 joueurs).</summary>
        public const int MaxPlayers = 4;

        private const uint AppId = 480;
        private const string HostSteamIdKey = "HostSteamId";

        public enum LobbyState
        {
            Idle,
            CreatingLobby,
            InLobby,
            Connecting,
            Failed
        }

        [SerializeField, Tooltip("Bootstrap réseau à piloter (choix transport + démarrage).")]
        private NetworkBootstrap _bootstrap;

        /// <summary>État courant, pour l'UI.</summary>
        public LobbyState State { get; private set; } = LobbyState.Idle;

        /// <summary>Dernier message d'erreur/statut lisible (vide si aucun).</summary>
        public string StatusMessage { get; private set; } = string.Empty;

        /// <summary>LobbyId courant sous forme de code (ulong en texte), vide si hors lobby.</summary>
        public string LobbyCode { get; private set; } = string.Empty;

        /// <summary>Sommes-nous l'hôte du lobby courant ?</summary>
        public bool IsHost { get; private set; }

        /// <summary>Notifié à chaque changement d'état (l'UI se rafraîchit).</summary>
        public event Action StateChanged;

        private Lobby _currentLobby;
        private bool _hasLobby;
        private bool _callbacksHooked;
        // LobbyId reçu via +connect_lobby au lancement, à rejoindre une fois Steam prêt.
        private ulong _pendingConnectLobby;

        private void Start()
        {
            if (_bootstrap == null)
                _bootstrap = FindAnyObjectByType<NetworkBootstrap>();

            // Lancement via l'overlay Steam avec le jeu fermé : Steam passe +connect_lobby <id>.
            _pendingConnectLobby = ParseConnectLobbyArg();
            if (_pendingConnectLobby != 0UL)
                JoinByCode(_pendingConnectLobby.ToString());
        }

        private void OnDestroy()
        {
            UnhookCallbacks();
            LeaveLobbyInternal();
        }

        // ---- API publique (appelée par LobbyPanel) --------------------------------------

        /// <summary>« Jouer solo » : host Tugboat local, sans Steam.</summary>
        public void StartSolo()
        {
            if (_bootstrap == null)
            {
                Fail("Bootstrap réseau introuvable.");
                return;
            }
            SetState(LobbyState.Connecting, "Démarrage solo…");
            _bootstrap.StartHost(NetworkBootstrap.TransportKind.Tugboat);
        }

        /// <summary>« Héberger (Steam) » : init Steam, crée un lobby amis-only, démarre le host FishyFacepunch.</summary>
        public async void HostSteam()
        {
            if (!EnsureSteamReady())
                return;

            // Poser IsHost AVANT tout await : l'hôte entre dans son propre lobby, ce qui
            // déclenche OnLobbyEntered pendant l'await de CreateLobbyAsync. Sans ce flag posé
            // tôt, HandleLobbyEntered pourrait partir en StartClientTo (client vers soi-même)
            // si le callback s'exécute avant la continuation. Reset si la création échoue.
            IsHost = true;

            HookCallbacks();
            SetState(LobbyState.CreatingLobby, "Création du lobby Steam…");

            try
            {
                Lobby? maybe = await SteamMatchmaking.CreateLobbyAsync(MaxPlayers);
                if (!maybe.HasValue)
                {
                    IsHost = false;
                    Fail("Échec de création du lobby Steam.");
                    return;
                }

                Lobby lobby = maybe.Value;
                _currentLobby = lobby;
                _hasLobby = true;

                ulong hostSteamId = SteamClient.SteamId.Value;
                lobby.SetFriendsOnly();
                lobby.SetJoinable(true);
                lobby.SetData(HostSteamIdKey, hostSteamId.ToString());
                lobby.SetData("game", "redshift");

                LobbyCode = lobby.Id.Value.ToString();
                SetState(LobbyState.InLobby, $"Lobby créé. Code : {LobbyCode}");

                // Démarrer le host sur le transport Steam (le NM s'active avec FishyFacepunch).
                if (!_bootstrap.StartHost(NetworkBootstrap.TransportKind.Steam, MaxPlayers))
                {
                    IsHost = false;
                    Fail("Impossible de démarrer le serveur FishNet (Steam).");
                    return;
                }
            }
            catch (Exception e)
            {
                IsHost = false;
                Fail($"Erreur host Steam : {e.Message}");
            }
        }

        /// <summary>Rejoindre un lobby par code (LobbyId ulong en texte).</summary>
        public async void JoinByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !ulong.TryParse(code.Trim(), out ulong lobbyId))
            {
                Fail("Code invalide (attendu : identifiant numérique du lobby).");
                return;
            }
            if (!EnsureSteamReady())
                return;

            HookCallbacks();
            SetState(LobbyState.Connecting, $"Connexion au lobby {lobbyId}…");

            try
            {
                // JoinLobbyAsync renvoie le Lobby rejoint (null si échec) ET déclenche
                // OnLobbyEntered en cas de succès → c'est HandleLobbyEntered qui câble ensuite
                // la connexion transport côté client.
                Lobby? joined = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
                if (!joined.HasValue)
                {
                    Fail("Impossible de rejoindre le lobby (introuvable, plein ou fermé).");
                }
            }
            catch (Exception e)
            {
                Fail($"Erreur de connexion au lobby : {e.Message}");
            }
        }

        /// <summary>Ouvre l'overlay Steam d'invitation d'amis pour le lobby courant.</summary>
        public void OpenInviteOverlay()
        {
            if (!_hasLobby)
            {
                StatusMessage = "Aucun lobby à partager.";
                RaiseChanged();
                return;
            }
            SteamFriends.OpenGameInviteOverlay(_currentLobby.Id);
        }

        // ---- Steam callbacks ------------------------------------------------------------

        private void HookCallbacks()
        {
            if (_callbacksHooked)
                return;
            SteamMatchmaking.OnLobbyEntered += HandleLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
            _callbacksHooked = true;
        }

        private void UnhookCallbacks()
        {
            if (!_callbacksHooked)
                return;
            SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
            _callbacksHooked = false;
        }

        /// <summary>Invitation acceptée depuis la liste d'amis (jeu déjà lancé).</summary>
        private void HandleGameLobbyJoinRequested(Lobby lobby, SteamId friend)
        {
            JoinByCode(lobby.Id.Value.ToString());
        }

        /// <summary>Entrée effective dans un lobby (host ou client) : câbler le transport côté client.</summary>
        private void HandleLobbyEntered(Lobby lobby)
        {
            _currentLobby = lobby;
            _hasLobby = true;
            LobbyCode = lobby.Id.Value.ToString();

            // L'hôte a déjà démarré son serveur dans HostSteam ; ne rien reconnecter.
            if (IsHost)
                return;

            string hostSteamId = lobby.GetData(HostSteamIdKey);
            if (string.IsNullOrEmpty(hostSteamId))
            {
                // Repli : l'owner du lobby est l'hôte.
                hostSteamId = lobby.Owner.Id.Value.ToString();
            }

            SetState(LobbyState.Connecting, $"Connexion à l'hôte {hostSteamId}…");

            if (_bootstrap == null)
            {
                Fail("Bootstrap réseau introuvable.");
                return;
            }
            // Client-only sur le transport Steam ; l'adresse = SteamID64 de l'hôte.
            if (!_bootstrap.StartClientTo(NetworkBootstrap.TransportKind.Steam, hostSteamId))
                Fail("Impossible de démarrer le client FishNet (Steam).");
        }

        // ---- Interne --------------------------------------------------------------------

        /// <summary>Init Steam à la demande, gardé. Renvoie false et passe en Failed si KO.</summary>
        private bool EnsureSteamReady()
        {
            if (SteamClient.IsValid)
                return true;
            try
            {
                SteamClient.Init(AppId, true);
            }
            catch (Exception e)
            {
                Fail($"Steam indisponible : {e.Message}");
                return false;
            }
            if (!SteamClient.IsValid)
            {
                Fail("Steam n'a pas pu s'initialiser (client Steam lancé ?).");
                return false;
            }
            return true;
        }

        private void LeaveLobbyInternal()
        {
            if (_hasLobby)
            {
                try { _currentLobby.Leave(); } catch { /* best effort */ }
                _hasLobby = false;
            }
        }

        private static ulong ParseConnectLobbyArg()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "+connect_lobby", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
                    ulong.TryParse(args[i + 1], out ulong id))
                    return id;
            }
            return 0UL;
        }

        private void Fail(string message)
        {
            SetState(LobbyState.Failed, message);
            Debug.LogError($"[SteamLobbyService] {message}");
        }

        private void SetState(LobbyState state, string message)
        {
            State = state;
            StatusMessage = message;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
