using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Visée + interaction du propriétaire : raycast depuis la tête, prompt, pickup/drop/slots.
    /// Activé uniquement chez le propriétaire (via PlayerMotor).
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform _head;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private InteractionDef _interaction;
        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField] private LayerMask _mask = ~0;

        private InputAction interactAction;
        private InputAction dropAction;
        private readonly InputAction[] slotActions = new InputAction[PlayerInventory.SlotCount];

        public PlayerInventory Inventory => _inventory;

        /// <summary>Cible visée ce frame (affichée par le HUD) ; null hors playmode/portée.</summary>
        public IInteractable CurrentTarget { get; private set; }

        private void OnEnable()
        {
            InputActionMap map = _inputAsset.FindActionMap("Player", throwIfNotFound: true);
            interactAction = map.FindAction("Interact", throwIfNotFound: true);
            dropAction = map.FindAction("Drop", throwIfNotFound: true);
            for (int i = 0; i < PlayerInventory.SlotCount; i++)
                slotActions[i] = map.FindAction($"Slot{i + 1}", throwIfNotFound: true);
        }

        private void Update()
        {
            CurrentTarget = FindTarget();

            if (interactAction.WasPressedThisFrame() && CurrentTarget != null && CurrentTarget.CanInteract(this))
                CurrentTarget.Interact(this);

            if (dropAction.WasPressedThisFrame() && _inventory.HandItem != null)
                _inventory.RequestDrop();

            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                if (slotActions[i].WasPressedThisFrame() && (_inventory.HandItem != null || _inventory.GetSlot(i) != null))
                    _inventory.RequestSlotSwap(i);
            }
        }

        private IInteractable FindTarget()
        {
            if (!Physics.Raycast(_head.position, _head.forward, out RaycastHit hit, _interaction.Range, _mask, QueryTriggerInteraction.Ignore))
                return null;
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        private void OnDisable() => CurrentTarget = null;
    }
}
