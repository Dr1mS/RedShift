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
    /// Solo/dev (D8 : multijoueur-first même en solo) : défaut Tugboat + FishyFacepunch
    /// laissé <c>enabled = false</c> et jamais assigné → <c>SteamClient.Init</c> n'est
    /// jamais appelé, le solo ne dépend pas de Steam. Le lobby Steam (P4-3) remplacera ce
    /// pilotage par ligne de commande.
    ///
    /// Arguments de ligne de commande (dev) :
    ///   -transport tugboat|steam   (défaut : tugboat)
    ///   -connect &lt;SteamID64 ou ip&gt;  (client seulement ; désactive l'auto-host)
    /// </summary>
    [DefaultExecutionOrder(short.MinValue)]
    public class NetworkBootstrap : MonoBehaviour
    {
        private enum TransportKind
        {
            Tugboat,
            Steam
        }

        [SerializeField, Tooltip("GameObject portant le NetworkManager (laissé désactivé dans la scène).")]
        private GameObject _networkManagerObject;

        [SerializeField, Tooltip("Auto-host au lancement si hors-ligne (dev solo). Ignoré si -connect est fourni.")]
        private bool _autoHostIfOffline = true;

        [SerializeField, Tooltip("DEBUG éditeur uniquement : force le transport en playmode (pas d'args CLI en éditeur). Laisser Tugboat pour le solo.")]
        private TransportKind _editorForceTransport = TransportKind.Tugboat;

        // Résolu au moment de l'Awake, utilisé par Start.
        private bool _startAsClientOnly;
        private string _connectAddress;

        private void Awake()
        {
            if (_networkManagerObject == null)
            {
                Debug.LogError("[NetworkBootstrap] _networkManagerObject non assigné. Le NetworkManager ne sera pas démarré.");
                return;
            }

            // 1) Déterminer le transport voulu et l'éventuelle adresse client.
            TransportKind kind = ResolveTransportKind(out _connectAddress);
            _startAsClientOnly = _connectAddress != null;

            // 2) Récupérer / créer le TransportManager sur le GO (inactif) du NetworkManager.
            //    Sur un GO inactif, AddComponent diffère l'Awake du composant jusqu'à SetActive(true).
            //    GetOrCreateComponent<TransportManager> côté NetworkManager réutilisera celui-ci.
            TransportManager tm = _networkManagerObject.GetComponent<TransportManager>();
            if (tm == null)
                tm = _networkManagerObject.AddComponent<TransportManager>();

            // 3) Récupérer les transports présents sur le GO.
            Tugboat tugboat = _networkManagerObject.GetComponent<Tugboat>();
            Transport steam = FindSteamTransport(_networkManagerObject);

            if (tugboat == null)
                Debug.LogError("[NetworkBootstrap] Aucun Tugboat sur le NetworkManager.");

            // 4) Sélectionner le transport actif AVANT l'init du NetworkManager.
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
                    if (_connectAddress != null)
                        steam.SetClientAddress(_connectAddress);
                    Debug.Log("[NetworkBootstrap] Transport = Steam (FishyFacepunch).");
                }
            }

            if (kind == TransportKind.Tugboat)
            {
                // Laisser le transport Steam inerte : pas d'assignation, pas d'Update() (SteamClient.Init jamais appelé).
                if (steam != null)
                    steam.enabled = false;
                if (tugboat != null)
                {
                    tm.Transport = tugboat;
                    if (_connectAddress != null)
                        tugboat.SetClientAddress(_connectAddress);
                }
                Debug.Log("[NetworkBootstrap] Transport = Tugboat.");
            }

            // 5) Activer le GO du NetworkManager : son Awake tourne maintenant, synchrone,
            //    avec le bon transport déjà assigné.
            if (!_networkManagerObject.activeSelf)
                _networkManagerObject.SetActive(true);
        }

        private void Start()
        {
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
