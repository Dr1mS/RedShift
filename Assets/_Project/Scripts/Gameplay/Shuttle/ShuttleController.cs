using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Navette 2 places, vol 6DOF assisté (SPEC §4.3). Owner-authoritative : le pilote
    /// possède le NetworkObject (transfert AVANT parentage, géré par <see cref="Seat"/>).
    /// Sans pilote, le host simule (chute/repos). Flight assist : amortissement vers la
    /// vitesse cible + auto-level près du sol, désactivable (X).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(GravityReceiver))]
    public class ShuttleController : NetworkBehaviour
    {
        [SerializeField] private ShuttleDef _def;
        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField, Tooltip("Couches sol pour l'auto-level et l'auto-snap.")]
        private LayerMask _groundMask = ~0;

        private readonly SyncVar<bool> _landed = new(true);

        private Rigidbody body;
        private GravityReceiver gravity;
        private ShuttleHull hull;

        private InputAction throttleAction;
        private InputAction strafeAction;
        private InputAction verticalAction;
        private InputAction lookAction;
        private InputAction rollAction;
        private InputAction assistAction;

        private bool assistEnabled = true;
        private Vector2 pendingLook;
        private float pendingRoll;

        public bool IsLanded => _landed.Value;
        public bool AssistEnabled => assistEnabled;
        public ShuttleDef Def => _def;
        public bool HasPilot { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            gravity = GetComponent<GravityReceiver>();
            hull = GetComponent<ShuttleHull>();
            body.useGravity = false;
            gravity.ApplyToBody = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyControlState();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyControlState();
        }

        /// <summary>Kinématique partout sauf chez le contrôleur (pilote, ou host sans pilote).</summary>
        private void ApplyControlState()
        {
            body.isKinematic = !IsController;
        }

        /// <summary>Appelé par le siège pilote quand le pilote local s'assoit/se lève.</summary>
        public void SetLocalPilot(bool piloting)
        {
            HasPilot = piloting;
            InputActionMap map = _inputAsset.FindActionMap("Flight", throwIfNotFound: true);
            if (piloting)
            {
                throttleAction = map.FindAction("Throttle", throwIfNotFound: true);
                strafeAction = map.FindAction("Strafe", throwIfNotFound: true);
                verticalAction = map.FindAction("Vertical", throwIfNotFound: true);
                lookAction = map.FindAction("Look", throwIfNotFound: true);
                rollAction = map.FindAction("Roll", throwIfNotFound: true);
                assistAction = map.FindAction("AssistToggle", throwIfNotFound: true);
                map.Enable();
            }
            else
            {
                map.Disable();
                throttleAction = null;
            }
        }

        private void Update()
        {
            if (!HasPilot || throttleAction == null)
                return;
            pendingLook += lookAction.ReadValue<Vector2>();
            pendingRoll = rollAction.ReadValue<float>();
            if (assistAction.WasPressedThisFrame())
                assistEnabled = !assistEnabled;
        }

        private void FixedUpdate()
        {
            if (!IsController)
                return;

            gravity.Refresh();

            if (_landed.Value)
            {
                if (HasPilot && WantsTakeoff())
                {
                    if (IsServerInitialized) _landed.Value = false;
                    else SetLandedServerRpc(false);
                    body.WakeUp();
                }
                else
                {
                    return;
                }
            }

            Fly();
        }

        private bool WantsTakeoff()
            => verticalAction != null && (verticalAction.ReadValue<float>() > 0.3f || Mathf.Abs(throttleAction.ReadValue<float>()) > 0.3f);

        private void Fly()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 gravityUp = gravity.Up;
            bool inGravity = gravity.InGravity;

            // --- Translation
            float throttle = throttleAction?.ReadValue<float>() ?? 0f;
            float strafe = strafeAction?.ReadValue<float>() ?? 0f;
            float vertical = verticalAction?.ReadValue<float>() ?? 0f;

            Vector3 targetVelocity = transform.forward * (throttle * _def.MaxForwardSpeed)
                                   + transform.right * (strafe * _def.MaxStrafeSpeed)
                                   + transform.up * (vertical * _def.MaxVerticalSpeed);

            Vector3 velocity = body.linearVelocity;
            if (assistEnabled)
            {
                // Assist : converge vers la vitesse cible ; sans input, amortit vers zéro
                // (et compense donc la gravité en vol stationnaire).
                float rate = HasInput(throttle, strafe, vertical) ? _def.LinearAcceleration : _def.AssistDamping * velocity.magnitude + 1f;
                velocity = Vector3.MoveTowards(velocity, targetVelocity, rate * dt);
            }
            else
            {
                velocity += (transform.forward * throttle + transform.right * strafe + transform.up * vertical) * (_def.LinearAcceleration * dt);
                if (inGravity)
                    velocity += gravity.CurrentAcceleration * dt;
            }
            body.linearVelocity = velocity;

            // --- Rotation
            Vector3 angularTarget = transform.right * (-pendingLook.y * _def.PitchSpeed * Mathf.Deg2Rad)
                                  + transform.up * (pendingLook.x * _def.YawSpeed * Mathf.Deg2Rad)
                                  + transform.forward * (-pendingRoll * _def.RollSpeed * Mathf.Deg2Rad);
            pendingLook = Vector2.zero;
            float angT = 1f - Mathf.Exp(-_def.AngularSharpness * dt);
            body.angularVelocity = Vector3.Lerp(body.angularVelocity, angularTarget, angT);

            // --- Auto-level près du sol (assist uniquement, en champ de gravité)
            if (assistEnabled && inGravity && Physics.Raycast(body.position, -gravityUp, out RaycastHit levelHit, _def.AutoLevelAltitude, _groundMask, QueryTriggerInteraction.Ignore))
            {
                float t = 1f - Mathf.Exp(-_def.AutoLevelSharpness * dt);
                Quaternion leveled = Quaternion.FromToRotation(transform.up, gravityUp) * body.rotation;
                body.MoveRotation(Quaternion.Slerp(body.rotation, leveled, t));
                TryAutoSnap(levelHit, gravityUp);
            }
        }

        private static bool HasInput(float throttle, float strafe, float vertical)
            => Mathf.Abs(throttle) > 0.05f || Mathf.Abs(strafe) > 0.05f || Mathf.Abs(vertical) > 0.05f;

        private void TryAutoSnap(RaycastHit groundHit, Vector3 gravityUp)
        {
            if (groundHit.distance > _def.SnapDistance)
                return;

            float approachSpeed = Vector3.Dot(body.linearVelocity, -gravityUp);
            float tilt = LandingEvaluator.TiltDegrees(transform.up, groundHit.normal);
            if (approachSpeed < 0.5f || !LandingEvaluator.CanSnap(approachSpeed, tilt, _def))
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.MoveRotation(Quaternion.FromToRotation(transform.up, groundHit.normal) * body.rotation);
            body.Sleep();

            if (IsServerInitialized) _landed.Value = true;
            else SetLandedServerRpc(true);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsController || hull == null)
                return;
            float damage = LandingEvaluator.ImpactDamage(collision.relativeVelocity.magnitude, _def);
            if (damage > 0f)
                hull.RequestDamage(damage);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SetLandedServerRpc(bool landed) => _landed.Value = landed;
    }
}
