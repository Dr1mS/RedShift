using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Siège de navette (SPEC §4.3) : le joueur assis est parenté au siège, son controller gelé.
    /// PIÈGE CONNU : pour le siège pilote, l'ownership de la navette est transféré au pilote
    /// AVANT le parentage réseau — sinon fight d'autorité.
    /// </summary>
    public class Seat : NetworkBehaviour, IInteractable
    {
        [SerializeField] private bool _isPilot;
        [SerializeField, Tooltip("Position du joueur assis.")]
        private Transform _anchor;
        [SerializeField, Tooltip("Position de sortie (côté navette).")]
        private Transform _exitPoint;
        [SerializeField] private ShuttleController _shuttle;
        [SerializeField] private InputActionAsset _inputAsset;

        private readonly SyncVar<NetworkObject> _occupant = new();

        private InputAction interactAction;
        private float localSeatTime;
        // Suivi local : la passe client du SyncVar peut livrer un `prev` déjà null,
        // on reconstitue donc la transition nous-mêmes.
        private NetworkObject lastKnownOccupant;

        public bool IsOccupied => _occupant.Value != null;
        public bool IsPilotSeat => _isPilot;
        public string Prompt => _isPilot ? "S'asseoir (pilote)" : "S'asseoir (passager)";

        private void Awake()
        {
            _occupant.OnChange += OnOccupantChanged;
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            var hull = _shuttle.GetComponent<ShuttleHull>();
            return !IsOccupied && (hull == null || !hull.IsDestroyed);
        }

        public void Interact(PlayerInteractor interactor)
        {
            var player = interactor.GetComponentInParent<NetworkObject>();
            if (player != null)
                SitServerRpc(player);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SitServerRpc(NetworkObject player, NetworkConnection sender = null)
        {
            if (player == null || IsOccupied || player.Owner != sender)
                return;
            var hull = _shuttle.GetComponent<ShuttleHull>();
            if (hull != null && hull.IsDestroyed)
                return;

            // Ownership de la navette au pilote AVANT le parentage (piège connu SPEC).
            if (_isPilot)
                _shuttle.NetworkObject.GiveOwnership(sender);

            player.SetParent(this);
            _occupant.Value = player;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ExitServerRpc(NetworkConnection sender = null)
        {
            NetworkObject player = _occupant.Value;
            if (player == null || player.Owner != sender)
                return;
            ServerUnseat(player);
        }

        [Server]
        private void ServerUnseat(NetworkObject player)
        {
            player.UnsetParent();
            if (_isPilot)
                _shuttle.NetworkObject.RemoveOwnership();
            _occupant.Value = null;
        }

        /// <summary>Destruction de la coque : éjecte l'occupant avec une impulsion (ragdoll en P3).</summary>
        [Server]
        public void ServerEject(float ejectionSpeed)
        {
            NetworkObject player = _occupant.Value;
            if (player == null)
                return;
            ServerUnseat(player);
            EjectTargetRpc(player.Owner, player, ejectionSpeed);
        }

        [TargetRpc]
        private void EjectTargetRpc(NetworkConnection conn, NetworkObject player, float ejectionSpeed)
        {
            var body = player.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
                body.linearVelocity = (transform.up * 2f + Random.insideUnitSphere).normalized * ejectionSpeed;
        }

        private void Update()
        {
            NetworkObject occupant = _occupant.Value;
            if (occupant == null || !occupant.IsOwner)
                return;

            // Anti double-déclenchement : le E qui assoit ne doit pas ressortir le même instant.
            if (Time.time - localSeatTime < 0.4f)
                return;

            interactAction ??= _inputAsset.FindActionMap("Player", throwIfNotFound: true).FindAction("Interact", throwIfNotFound: true);
            if (interactAction.WasPressedThisFrame())
                ExitServerRpc();
        }

        private void OnOccupantChanged(NetworkObject prev, NetworkObject next, bool asServer)
        {
            // Le host reçoit aussi la passe client ; on n'exécute la logique visuelle/état qu'une fois.
            if (asServer && IsClientInitialized)
                return;
            if (lastKnownOccupant == next)
                return;

            if (lastKnownOccupant != null)
                HandleUnseat(lastKnownOccupant);
            lastKnownOccupant = next;
            if (next != null)
                HandleSeat(next);
        }

        private void HandleSeat(NetworkObject player)
        {
            var motor = player.GetComponent<PlayerMotor>();
            motor.SetSeated(true, _isPilot);

            if (player.IsOwner)
            {
                localSeatTime = Time.time;
                player.transform.SetPositionAndRotation(_anchor.position, _anchor.rotation);
                if (_isPilot)
                    _shuttle.SetLocalPilot(true);
            }
        }

        private void HandleUnseat(NetworkObject player)
        {
            var motor = player.GetComponent<PlayerMotor>();
            motor.SetSeated(false, _isPilot);

            if (player.IsOwner)
            {
                player.transform.SetPositionAndRotation(_exitPoint.position, _exitPoint.rotation);
                if (_isPilot)
                    _shuttle.SetLocalPilot(false);
            }
        }
    }
}
