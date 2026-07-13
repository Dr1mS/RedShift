using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Item physique dans le monde. Rigidbody endormi par défaut, réveillé à l'interaction,
    /// autorité host (SPEC §5 / piège connu « loot physique »). Chez les clients le rigidbody
    /// est kinématique : la position vient du NetworkTransform (server-authoritative).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WorldItem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ItemDef _def;

        private readonly SyncVar<PlayerInventory> _holder = new();
        private readonly SyncVar<bool> _stored = new();

        private Rigidbody body;
        private Collider[] colliders;
        private Renderer[] renderers;

        public ItemDef Def => _def;
        public PlayerInventory Holder => _holder.Value;
        public bool IsHeld => _holder.Value != null;
        public bool IsStored => _stored.Value;

        public string Prompt => $"Ramasser {_def.DisplayName}";

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            body.mass = _def != null ? _def.Mass : 1f;
            body.useGravity = false; // GravityReceiver s'en charge.

            _holder.OnChange += OnHolderChanged;
            _stored.OnChange += OnStoredChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            body.Sleep(); // Endormi par défaut (piège connu).
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized)
                body.isKinematic = true; // Autorité physique au host.
            ApplyHeldState();
        }

        public bool CanInteract(PlayerInteractor interactor) => !IsHeld && !IsStored;

        public void Interact(PlayerInteractor interactor) => interactor.Inventory.RequestPickup(this);

        /// <summary>Serveur uniquement : attache l'item à un inventaire (main).</summary>
        [Server]
        public void SetHolder(PlayerInventory inventory)
        {
            _holder.Value = inventory;
            _stored.Value = false;
        }

        /// <summary>Serveur uniquement : range/sort l'item d'un slot (invisible tant que rangé).</summary>
        [Server]
        public void SetStored(bool stored) => _stored.Value = stored;

        /// <summary>Serveur uniquement : relâche l'item avec une impulsion de lancer.</summary>
        [Server]
        public void Release(Vector3 position, Quaternion rotation, Vector3 tossVelocity)
        {
            _holder.Value = null;
            _stored.Value = false;
            transform.SetPositionAndRotation(position, rotation);
            body.WakeUp();
            body.linearVelocity = tossVelocity;
        }

        private void OnHolderChanged(PlayerInventory prev, PlayerInventory next, bool asServer) => ApplyHeldState();

        private void OnStoredChanged(bool prev, bool next, bool asServer) => ApplyHeldState();

        private void ApplyHeldState()
        {
            bool held = IsHeld;
            bool visible = !IsStored;

            foreach (Collider c in colliders)
                c.enabled = !held && visible;
            foreach (Renderer r in renderers)
                r.enabled = visible;

            // Kinématique si tenu/rangé ; sinon la physique ne tourne que sur le host.
            body.isKinematic = held || IsStored || !IsServerInitialized;
        }

        private void LateUpdate()
        {
            // Suivi de la main localement chez tous les pairs : lisse et sans coût réseau.
            if (IsHeld && !IsStored && _holder.Value.HandAnchor != null)
                transform.SetPositionAndRotation(_holder.Value.HandAnchor.position, _holder.Value.HandAnchor.rotation);
        }
    }
}
