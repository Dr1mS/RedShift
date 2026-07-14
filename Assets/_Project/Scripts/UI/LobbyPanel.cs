using Redshift.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Redshift.UI
{
    /// <summary>
    /// Panneau lobby minimal (P4-3) affiché au lancement en build : « Jouer solo »,
    /// « Héberger (Steam) », « Rejoindre » + champ code, bouton « Inviter » visible en
    /// lobby, label d'état/erreur. Se masque dès qu'une connexion est en cours/établie.
    ///
    /// Le panneau s'affiche ssi le NetworkBootstrap a différé le démarrage
    /// (<c>DeferredForLobby</c>) : source de vérité unique, jamais divergents. En éditeur,
    /// c'est le champ debug <c>_editorForcePanel</c> du bootstrap qui décide (défaut :
    /// auto-host Tugboat, panneau masqué) ; en build, son champ <c>_lobbyDrivenStartup</c>.
    /// </summary>
    public class LobbyPanel : MonoBehaviour
    {
        [SerializeField] private SteamLobbyService _service;
        [SerializeField] private NetworkBootstrap _bootstrap;
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _codeText;
        [SerializeField] private Button _soloButton;
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _joinButton;
        [SerializeField] private Button _inviteButton;
        [SerializeField] private InputField _codeInput;

        private bool _dismissed;

        private void Awake()
        {
            if (_service == null)
                _service = FindAnyObjectByType<SteamLobbyService>();
            if (_bootstrap == null)
                _bootstrap = FindAnyObjectByType<NetworkBootstrap>();

            // Visible ssi le bootstrap a différé le démarrage pour nous laisser choisir.
            bool show = _bootstrap != null && _bootstrap.DeferredForLobby;
            if (_root != null)
                _root.SetActive(show);
            if (!show)
                _dismissed = true;

            if (_soloButton != null) _soloButton.onClick.AddListener(OnSolo);
            if (_hostButton != null) _hostButton.onClick.AddListener(OnHost);
            if (_joinButton != null) _joinButton.onClick.AddListener(OnJoin);
            if (_inviteButton != null) _inviteButton.onClick.AddListener(OnInvite);
        }

        private void OnEnable()
        {
            if (_service != null)
                _service.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            if (_service != null)
                _service.StateChanged -= Refresh;
        }

        private void Start()
        {
            Refresh();
        }

        private void Update()
        {
            // Le réseau démarre quelques frames après le passage en Connecting, sans nouvel
            // événement StateChanged : sonder ici pour masquer le panneau une fois connecté
            // (solo ou client). Inactif dès que le panneau est masqué ou en lobby Steam hôte.
            if (_dismissed || _service == null)
                return;
            if (_service.State == SteamLobbyService.LobbyState.Connecting && !_service.IsHost && NetworkStarted())
                Dismiss();
        }

        private void OnSolo()
        {
            if (_service != null)
                _service.StartSolo();
        }

        private void OnHost()
        {
            if (_service != null)
                _service.HostSteam();
        }

        private void OnJoin()
        {
            if (_service != null && _codeInput != null)
                _service.JoinByCode(_codeInput.text);
        }

        private void OnInvite()
        {
            if (_service != null)
                _service.OpenInviteOverlay();
        }

        private void Refresh()
        {
            if (_service == null || _dismissed)
                return;

            if (_statusText != null)
                _statusText.text = _service.StatusMessage;

            bool inLobby = _service.State == SteamLobbyService.LobbyState.InLobby;
            if (_codeText != null)
            {
                _codeText.text = inLobby && !string.IsNullOrEmpty(_service.LobbyCode)
                    ? $"CODE : {_service.LobbyCode}"
                    : string.Empty;
            }

            // Bouton Inviter visible seulement quand on héberge un lobby.
            if (_inviteButton != null)
                _inviteButton.gameObject.SetActive(inLobby && _service.IsHost);

            // Solo (Tugboat) ou client connecté : retirer les boutons pour éviter les doubles
            // clics pendant la connexion, puis masquer entièrement le panneau une fois le
            // réseau local démarré (le jeu prend le dessus). L'hôte Steam garde le panneau
            // visible pour afficher le code et inviter.
            if (_service.State == SteamLobbyService.LobbyState.Connecting && !_service.IsHost)
            {
                SetActionButtonsInteractable(false);
                if (NetworkStarted())
                    Dismiss();
            }
        }

        /// <summary>Le NetworkManager local a-t-il démarré (server ou client) ?</summary>
        private static bool NetworkStarted()
        {
            var nm = FishNet.InstanceFinder.NetworkManager;
            return nm != null && (nm.ServerManager.Started || nm.ClientManager.Started);
        }

        /// <summary>Masque définitivement le panneau (le jeu est lancé).</summary>
        private void Dismiss()
        {
            _dismissed = true;
            if (_root != null)
                _root.SetActive(false);
        }

        private void SetActionButtonsInteractable(bool on)
        {
            if (_soloButton != null) _soloButton.interactable = on;
            if (_hostButton != null) _hostButton.interactable = on;
            if (_joinButton != null) _joinButton.interactable = on;
        }
    }
}
