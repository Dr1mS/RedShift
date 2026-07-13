using FishNet;
using FishNet.Managing;
using UnityEngine;

namespace Redshift.Networking
{
    /// <summary>
    /// Démarre un host local (serveur + client) si aucune connexion n'existe.
    /// Flux solo/dev (D8 : multijoueur-first, même en test solo). Le lobby Steam (P4)
    /// remplacera ce chemin en désactivant l'auto-host.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("Auto-host au lancement si hors-ligne (dev solo).")]
        private bool _autoHostIfOffline = true;

        private void Start()
        {
            if (!_autoHostIfOffline)
                return;

            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm == null)
            {
                Debug.LogError("[NetworkBootstrap] Aucun NetworkManager dans la scène.");
                return;
            }

            if (!nm.ServerManager.Started && !nm.ClientManager.Started)
            {
                nm.ServerManager.StartConnection();
                nm.ClientManager.StartConnection();
            }
        }
    }
}
