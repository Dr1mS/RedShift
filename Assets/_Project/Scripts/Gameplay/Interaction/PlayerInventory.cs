using FishNet.Connection;
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

            // Déco du porteur : les WorldItems sont server-owned (jamais transférés au pickup),
            // donc FishNet ne les despawne PAS avec la connexion — mais rien ne les relâche non
            // plus. Sans ceci, l'objet en main reste kinématique/colliders off, figé en l'air à
            // la dernière position de la main (fantôme intouchable), et les objets en slot restent
            // invisibles (renderers off) et injoignables : NetworkObjects leakés. On les laisse
            // donc retomber au sol côté serveur AVANT le despawn des objets de la connexion.
            // Invoqué avant ServerObjects.ClientDisconnected (même fenêtre que Seat.cs).
            ServerManager.Objects.OnPreDestroyClientObjects += ServerOnPreDestroyClientObjects;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (ServerManager != null)
                ServerManager.Objects.OnPreDestroyClientObjects -= ServerOnPreDestroyClientObjects;
        }

        private void ServerOnPreDestroyClientObjects(NetworkConnection conn)
        {
            if (conn != Owner)
                return;
            ServerDropAllHeld();
        }

        /// <summary>Serveur : relâche au sol l'objet en main et tous les objets en slot (déco du porteur).</summary>
        [Server]
        private void ServerDropAllHeld()
        {
            Vector3 basePos = _handAnchor != null ? _handAnchor.position : transform.position;
            Quaternion rot = _handAnchor != null ? _handAnchor.rotation : transform.rotation;
            int released = 0;

            WorldItem hand = _handItem.Value;
            if (hand != null)
            {
                _handItem.Value = null;
                hand.Release(basePos, rot, Vector3.zero);
                released++;
            }

            // Petit décalage par slot pour ne pas empiler les items exactement au même point.
            for (int i = 0; i < _slots.Count; i++)
            {
                WorldItem inSlot = _slots[i];
                if (inSlot == null)
                    continue;
                _slots[i] = null;
                Vector3 offset = new Vector3(0.15f * (i + 1), 0f, 0f);
                inSlot.Release(basePos + offset, rot, Vector3.zero);
                released++;
            }

            if (released > 0)
                Debug.Log($"[PlayerInventory] Porteur déconnecté : {released} objet(s) relâché(s) au sol (anti frozen-ghost).");
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
