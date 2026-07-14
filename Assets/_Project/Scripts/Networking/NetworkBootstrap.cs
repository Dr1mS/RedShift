using System;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace Redshift.Networking
{
    /// <summary>
    /// Sélectionne le transport (Tugboat par défaut, Steam optionnel) AVANT que le
    /// NetworkManager ne s'initialise, puis démarre un host local (ou un client si
    /// <c>-connect</c> est fourni).
    ///
    /// Le NetworkManager fait toute son init dans son <c>Awake</c> à
    /// <c>DefaultExecutionOrder(short.MinValue)</c> : impossible de s'exécuter avant lui
    /// par ordre d'exécution seul. Le GameObject du NetworkManager est donc laissé
    /// DÉSACTIVÉ dans la scène ; ce bootstrap (sur un GO séparé, actif) choisit le
    /// transport, l'assigne à <see cref="TransportManager.Transport"/>, puis active le GO
    /// du NetworkManager — ce qui déclenche son Awake de façon synchrone avec le bon
    /// transport déjà en place (bons abonnements ServerManager/ClientManager, bons MTU).
    ///
    /// IMPORTANT (P4-3) : le transport doit être choisi AVANT l'activation du NM. FishNet
    /// n'appelle <c>Transport.Initialize</c> et ne câble les events ServerManager/
    /// ClientManager qu'une seule fois, à l'activation. Réassigner
    /// <see cref="TransportManager.Transport"/> après coup ne ré-initialise pas le nouveau
    /// transport → serveur « Started » mais rien ne circule. On ne hot-swap donc jamais :
    /// tant qu'aucun mode n'est choisi (build avec panneau lobby), le GO du NM reste
    /// désactivé et <see cref="ActivateWith"/> fait la sélection + activation au moment du
    /// clic. Le service lobby (P4-3) appelle ces méthodes publiques.
    ///
    /// Solo/dev (D8 : multijoueur-first même en solo) : défaut Tugboat + FishyFacepunch
    /// laissé <c>enabled = false</c> et jamais assigné → <c>SteamClient.Init</c> n'est
    /// jamais appelé par le transport, le solo ne dépend pas de Steam.
    ///
    /// Arguments de ligne de commande (dev) :
    ///   -transport tugboat|steam   (défaut : tugboat)
    ///   -connect &lt;SteamID64 ou ip&gt;  (client seulement ; désactive l'auto-host)
    /// </summary>
    [DefaultExecutionOrder(short.MinValue)]
    public class NetworkBootstrap : MonoBehaviour
    {
        public enum TransportKind
        {
            Tugboat,
            Steam
        }

        [SerializeField, Tooltip("GameObject portant le NetworkManager (laissé désactivé dans la scène).")]
        private GameObject _networkManagerObject;

        [SerializeField, Tooltip("Auto-host au lancement si hors-ligne (dev solo). Ignoré si -connect est fourni ou si le panneau lobby pilote le démarrage.")]
        private bool _autoHostIfOffline = true;

        [SerializeField, Tooltip("DEBUG éditeur uniquement : force le transport en playmode (pas d'args CLI en éditeur). Laisser Tugboat pour le solo.")]
        private TransportKind _editorForceTransport = TransportKind.Tugboat;

        [SerializeField, Tooltip("BUILD : si coché, le panneau lobby pilote le démarrage (auto-host désactivé, NM différé jusqu'au choix du mode). Sans effet en éditeur (voir _editorForcePanel).")]
        private bool _lobbyDrivenStartup;

        [SerializeField, Tooltip("DEBUG éditeur uniquement : forcer le pilotage par panneau lobby en playmode (sinon auto-host Tugboat immédiat pour l'itération dev).")]
        private bool _editorForcePanel;

        // Résolu au moment de l'Awake, utilisé par Start.
        private bool _startAsClientOnly;
        private string _connectAddress;

        /// <summary>Le NM a-t-il déjà été activé (transport figé) ?</summary>
        public bool NetworkManagerActivated { get; private set; }

        /// <summary>
        /// Le démarrage a-t-il été différé pour laisser un panneau lobby choisir le mode ?
        /// Calculé en Awake (source de vérité unique). Le LobbyPanel lit cette prop pour
        /// décider s'il s'affiche : panneau visible ⇔ démarrage différé (jamais divergents).
        /// </summary>
        public bool DeferredForLobby { get; private set; }

        private void Awake()
        {
            if (_networkManagerObject == null)
            {
                Debug.LogError("[NetworkBootstrap] _networkManagerObject non assigné. Le NetworkManager ne sera pas démarré.");
                return;
            }

            // Déterminer le transport voulu et l'éventuelle adresse client.
            TransportKind kind = ResolveTransportKind(out _connectAddress);
            _startAsClientOnly = _connectAddress != null;

            // Source de vérité unique pour « le panneau lobby pilote le démarrage » :
            //  - un arg CLI dev (-transport/-connect) l'emporte toujours (chemin dev direct) ;
            //  - en éditeur, seul _editorForcePanel active le panneau (défaut : auto-host Tugboat) ;
            //  - en build, c'est _lobbyDrivenStartup (coché sur la scène pour la distribution).
            bool panelWanted = Application.isEditor ? _editorForcePanel : _lobbyDrivenStartup;
            DeferredForLobby = panelWanted && !_startAsClientOnly && !HasTransportArg();

            if (DeferredForLobby)
            {
                Debug.Log("[NetworkBootstrap] Démarrage différé : le panneau lobby choisira le mode.");
                return;
            }

            // Chemin dev/solo/éditeur : figer le transport et activer le NM tout de suite.
            ActivateWith(kind, _connectAddress);
        }

        private void Start()
        {
            // Rien à faire tant que le NM n'a pas été activé (mode lobby en attente de clic).
            if (!NetworkManagerActivated)
                return;

            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm == null)
            {
                Debug.LogError("[NetworkBootstrap] Aucun NetworkManager après activation.");
                return;
            }

            if (nm.ServerManager.Started || nm.ClientManager.Started)
                return;

            if (_startAsClientOnly)
            {
                // -connect fourni : client uniquement (l'adresse a déjà été posée sur le transport en Awake).
                Debug.Log($"[NetworkBootstrap] Démarrage client vers '{_connectAddress}'.");
                nm.ClientManager.StartConnection();
                return;
            }

            if (_autoHostIfOffline)
            {
                nm.ServerManager.StartConnection();
                nm.ClientManager.StartConnection();
            }
        }

        /// <summary>
        /// Sélectionne le transport <paramref name="kind"/>, l'assigne au TransportManager,
        /// pose éventuellement l'adresse client, PUIS active le GO du NetworkManager (son
        /// Awake tourne alors synchrone avec le bon transport). Idempotent : ne fait rien si
        /// le NM est déjà activé (le transport ne doit jamais être hot-swappé après coup).
        /// </summary>
        public void ActivateWith(TransportKind kind, string connectAddress)
        {
            if (NetworkManagerActivated)
            {
                Debug.LogWarning("[NetworkBootstrap] ActivateWith ignoré : le NetworkManager est déjà activé (transport figé).");
                return;
            }
            if (_networkManagerObject == null)
            {
                Debug.LogError("[NetworkBootstrap] ActivateWith : _networkManagerObject non assigné.");
                return;
            }

            _connectAddress = connectAddress;
            _startAsClientOnly = connectAddress != null;

            // Récupérer / créer le TransportManager sur le GO (inactif) du NetworkManager.
            // Sur un GO inactif, AddComponent diffère l'Awake du composant jusqu'à SetActive(true).
            // GetOrCreateComponent<TransportManager> côté NetworkManager réutilisera celui-ci.
            TransportManager tm = _networkManagerObject.GetComponent<TransportManager>();
            if (tm == null)
                tm = _networkManagerObject.AddComponent<TransportManager>();

            Tugboat tugboat = _networkManagerObject.GetComponent<Tugboat>();
            Transport steam = FindSteamTransport(_networkManagerObject);

            if (tugboat == null)
                Debug.LogError("[NetworkBootstrap] Aucun Tugboat sur le NetworkManager.");

            if (kind == TransportKind.Steam)
            {
                if (steam == null)
                {
                    Debug.LogError("[NetworkBootstrap] Transport Steam demandé mais FishyFacepunch introuvable ; repli sur Tugboat.");
                    kind = TransportKind.Tugboat;
                }
                else
                {
                    steam.enabled = true;
                    tm.Transport = steam;
                    if (connectAddress != null)
                        steam.SetClientAddress(connectAddress);
                    Debug.Log("[NetworkBootstrap] Transport = Steam (FishyFacepunch).");
                }
            }

            if (kind == TransportKind.Tugboat)
            {
                // Laisser le transport Steam inerte : pas d'assignation, pas d'Update().
                if (steam != null)
                    steam.enabled = false;
                if (tugboat != null)
                {
                    tm.Transport = tugboat;
                    if (connectAddress != null)
                        tugboat.SetClientAddress(connectAddress);
                }
                Debug.Log("[NetworkBootstrap] Transport = Tugboat.");
            }

            // Activer le GO du NetworkManager : son Awake tourne maintenant, synchrone,
            // avec le bon transport déjà assigné.
            if (!_networkManagerObject.activeSelf)
                _networkManagerObject.SetActive(true);

            NetworkManagerActivated = true;
        }

        /// <summary>
        /// Active le transport voulu (si pas déjà fait) puis démarre un host (serveur + client
        /// local). Utilisé par le panneau lobby pour « Jouer solo » (Tugboat) et « Héberger
        /// Steam » (Steam). <paramref name="maxClients"/> &gt; 0 cape le transport (cohérence
        /// avec la taille du lobby). Retourne false si le NM est introuvable.
        /// </summary>
        public bool StartHost(TransportKind kind, int maxClients = 0)
        {
            ActivateWith(kind, null);
            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm == null)
            {
                Debug.LogError("[NetworkBootstrap] StartHost : aucun NetworkManager.");
                return false;
            }
            if (nm.ServerManager.Started)
                return true;
            nm.ServerManager.StartConnection();
            nm.ClientManager.StartConnection();
            // SetMaximumClients APRÈS StartConnection : le socket FishyFacepunch réécrit sa
            // limite depuis le field du transport (16) au démarrage ; poser la valeur ensuite
            // tient (relue à chaque connexion entrante). Le vrai plafond reste le lobby Steam
            // (CreateLobbyAsync(4)), ceci n'est qu'une cohérence côté transport.
            if (maxClients > 0 && nm.TransportManager.Transport != null)
                nm.TransportManager.Transport.SetMaximumClients(maxClients);
            return true;
        }

        /// <summary>
        /// Active le transport voulu (si pas déjà fait), pose l'adresse d'hôte puis démarre
        /// une connexion client uniquement. Utilisé par le panneau lobby pour rejoindre
        /// (Steam : <paramref name="connectAddress"/> = SteamID64 de l'hôte). Retourne false
        /// si le NM est introuvable.
        /// </summary>
        public bool StartClientTo(TransportKind kind, string connectAddress)
        {
            ActivateWith(kind, connectAddress);
            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm == null)
            {
                Debug.LogError("[NetworkBootstrap] StartClientTo : aucun NetworkManager.");
                return false;
            }
            if (nm.ClientManager.Started)
                return true;
            nm.ClientManager.StartConnection();
            return true;
        }

        /// <summary>
        /// Détermine le transport à utiliser. Priorité : args CLI (build) &gt; champ debug éditeur.
        /// Renvoie aussi l'adresse client si -connect est fourni (sinon null).
        /// </summary>
        private TransportKind ResolveTransportKind(out string connectAddress)
        {
            connectAddress = null;
            // En build, seuls les args CLI décident : le champ debug ne doit jamais faire
            // dépendre un build solo de Steam silencieusement.
            TransportKind kind = Application.isEditor ? _editorForceTransport : TransportKind.Tugboat;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "-transport", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    string v = args[i + 1];
                    if (string.Equals(v, "steam", StringComparison.OrdinalIgnoreCase))
                        kind = TransportKind.Steam;
                    else if (string.Equals(v, "tugboat", StringComparison.OrdinalIgnoreCase))
                        kind = TransportKind.Tugboat;
                }
                else if (string.Equals(a, "-connect", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    connectAddress = args[i + 1];
                }
            }

            return kind;
        }

        /// <summary>Un arg CLI -transport ou -connect est-il présent (chemin dev prioritaire) ?</summary>
        private static bool HasTransportArg()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-transport", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "-connect", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Trouve le transport Steam (FishyFacepunch) sur le GameObject sans référence de type
        /// à la dll (évite un couplage dur au cas où le transport serait retiré du projet).
        /// </summary>
        private static Transport FindSteamTransport(GameObject go)
        {
            foreach (Transport t in go.GetComponents<Transport>())
            {
                if (t.GetType().Name == "FishyFacepunch")
                    return t;
            }

            return null;
        }
    }
}
