using FishNet.Connection;
using FishNet.Object;
using Redshift.Core;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Cockpit de l'Arche-lite (SPEC §3.2/§4.4) : n'importe quel joueur peut amorcer le
    /// saut pendant la fenêtre d'Effondrement. Au saut (manuel ou auto à T-0), les
    /// joueurs vivants dans le rayon d'embarquement sont évacués — les autres restent
    /// face à l'onde. Le vote d'équipage arrive avec le multi (P4).
    /// </summary>
    public class JumpCockpit : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ArkDef _def;

        public string Prompt => "Amorcer le saut FTL";

        public bool CanInteract(PlayerInteractor interactor)
            => GameDirector.Instance != null && GameDirector.Instance.Phase == GamePhase.Collapse;

        public void Interact(PlayerInteractor interactor) => JumpServerRpc();

        [ServerRpc(RequireOwnership = false)]
        private void JumpServerRpc(NetworkConnection sender = null)
        {
            var director = GameDirector.Instance;
            if (director == null || !director.TryJumpNow())
                return;
            EvacuateBoarded();
        }

        /// <summary>Serveur : évacue les joueurs vivants à bord (appelé au saut, manuel ou T-0).</summary>
        [Server]
        public void EvacuateBoarded()
        {
            foreach (PlayerHealth player in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                if (player.IsDead || player.IsEvacuated)
                    continue;
                if (JumpRules.IsAboard(player.transform.position, transform.position, _def.BoardingRadius))
                    player.Evacuate();
            }
        }
    }
}
