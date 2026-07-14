using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Inventaire LC-style (SPEC §4.1) : 1 objet en main + 4 slots rapides.
    /// Toutes les mutations passent par le serveur (autorité host sur le loot).
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        public const int SlotCount = 4;

        [SerializeField, Tooltip("Ancre de la main : position des objets tenus.")]
        private Transform _handAnchor;
        [SerializeField] private InteractionDef _interaction;

        private readonly SyncVar<WorldItem> _handItem = new();
        private readonly SyncList<WorldItem> _slots = new();

        private PlayerHealth health;

        public Transform HandAnchor => _handAnchor;
        public WorldItem HandItem => _handItem.Value;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
        }

        // Un mort ne manipule plus le loot — y compris les RPC encore en vol au moment du décès.
        private bool ServerBlockedByDeath => health != null && health.IsDead;

        public WorldItem GetSlot(int index) => index >= 0 && index < _slots.Count ? _slots[index] : null;

        public override void OnStartServer()
        {
            base.OnStartServer();
            for (int i = _slots.Count; i < SlotCount; i++)
                _slots.Add(null);
        }

        public void RequestPickup(WorldItem item) => PickupServerRpc(item);

        public void RequestDrop() => DropServerRpc();

        /// <summary>Slot plein + main vide → équipe ; main pleine + slot vide → range ; les deux → échange.</summary>
        public void RequestSlotSwap(int slot) => SlotSwapServerRpc(slot);

        /// <summary>Serveur uniquement : vide la main sans relâcher l'item (dépôt en soute).</summary>
        [Server]
        public void ServerClearHand() => _handItem.Value = null;

        /// <summary>Serveur uniquement : lâche l'objet en main sur place (mort du joueur).</summary>
        [Server]
        public void ServerDropHand()
        {
            WorldItem item = _handItem.Value;
            if (item == null)
                return;
            _handItem.Value = null;
            item.Release(_handAnchor.position, _handAnchor.rotation, Vector3.zero);
        }

        [ServerRpc]
        private void PickupServerRpc(WorldItem item)
        {
            if (ServerBlockedByDeath || item == null || item.IsHeld || item.IsStored || _handItem.Value != null)
                return;
            item.SetHolder(this);
            _handItem.Value = item;
        }

        [ServerRpc]
        private void DropServerRpc()
        {
            WorldItem item = _handItem.Value;
            if (item == null)
                return;
            _handItem.Value = null;
            Vector3 toss = (_handAnchor.forward + _handAnchor.up * 0.2f).normalized * _interaction.TossSpeed;
            item.Release(_handAnchor.position, _handAnchor.rotation, toss);
        }

        [ServerRpc]
        private void SlotSwapServerRpc(int slot)
        {
            if (ServerBlockedByDeath || slot < 0 || slot >= SlotCount)
                return;

            WorldItem hand = _handItem.Value;
            WorldItem inSlot = _slots[slot];

            if (hand != null)
                hand.SetStored(true);
            if (inSlot != null)
                inSlot.SetStored(false);

            _slots[slot] = hand;
            _handItem.Value = inSlot;
        }
    }
}
