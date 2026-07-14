using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Soute de dépôt du quota (SPEC §3.2/§4.6) : seul le loot déposé ici compte.
    /// Le joueur interagit avec l'objet en main ; la valeur est créditée host-side
    /// au GameDirector puis l'item est despawné (soute physique/vente : P5).
    /// </summary>
    public class CargoDepositZone : NetworkBehaviour, IInteractable
    {
        public string Prompt => "Déposer en soute";

        public bool CanInteract(PlayerInteractor interactor)
            => interactor.Inventory != null && interactor.Inventory.HandItem != null;

        public void Interact(PlayerInteractor interactor) => DepositServerRpc(interactor.Inventory);

        [ServerRpc(RequireOwnership = false)]
        private void DepositServerRpc(PlayerInventory inventory, NetworkConnection sender = null)
        {
            if (inventory == null || inventory.Owner != sender)
                return;
            WorldItem item = inventory.HandItem;
            if (item == null)
                return;

            int value = item.Def != null ? item.Def.Value : 0;
            inventory.ServerClearHand();
            item.Despawn();

            if (GameDirector.Instance != null)
                GameDirector.Instance.DepositValue(value);
        }
    }
}
