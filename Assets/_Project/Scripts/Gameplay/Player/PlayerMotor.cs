using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Controller FPS rigidbody aligné sur la gravité locale (SPEC §4.1).
    /// Owner-authoritative (SPEC §5) : seul le propriétaire simule, les autres pairs
    /// reçoivent la position via NetworkTransform (rigidbody kinématique chez eux).
    /// L'up du corps suit la gravité en slerp — jamais de snap (piège connu).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(GravityReceiver))]
    public class PlayerMotor : NetworkBehaviour
    {
        [SerializeField] private PlayerMovementDef _config;
        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField, Tooltip("Transform tête (caméra) — piloté pour l'offset accroupi.")]
        private Transform _head;
        [SerializeField, Tooltip("Objets actifs uniquement chez le propriétaire (caméra, listener).")]
        private GameObject[] _ownerOnlyObjects;
        [SerializeField, Tooltip("Behaviours actifs uniquement chez le propriétaire (look, interactor…).")]
        private Behaviour[] _ownerOnlyBehaviours;
        [SerializeField, Tooltip("Couches considérées comme sol.")]
        private LayerMask _groundMask = ~0;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private GravityReceiver gravity;
        private PlayerInteractor interactor;
        private PlayerLook look;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;

        private float pendingYaw;
        private bool jumpQueued;
        private bool sprintExhausted;
        private float stamina;
        private float lastSprintTime = float.NegativeInfinity;
        private Vector3 groundNormal = Vector3.up;

        public bool IsGrounded { get; private set; }
        public bool IsCrouched { get; private set; }
        public bool IsSprinting { get; private set; }
        public float StaminaNormalized => _config == null ? 1f : stamina / _config.StaminaMax;
        public GravityReceiver Gravity => gravity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            gravity = GetComponent<GravityReceiver>();
            interactor = GetComponent<PlayerInteractor>();
            look = _head != null ? _head.GetComponent<PlayerLook>() : null;

            body.useGravity = false;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            // Le motor applique lui-même la gravité pour garder un seul point d'écriture vélocité.
            gravity.ApplyToBody = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            bool owner = IsOwner;

            foreach (GameObject go in _ownerOnlyObjects)
                go.SetActive(owner);
            foreach (Behaviour b in _ownerOnlyBehaviours)
                b.enabled = owner;

            if (!owner)
            {
                body.isKinematic = true;
                enabled = false;
                return;
            }

            stamina = _config.StaminaMax;
            BindInput();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner)
                _inputAsset.FindActionMap("Player")?.Disable();
        }

        /// <summary>
        /// Gèle/dégèle le controller quand le joueur s'assoit dans un siège (SPEC §4.3).
        /// Appelé chez TOUS les pairs (collider coupé partout) ; le pilote perd aussi
        /// le look tête (la souris pilote la navette).
        /// </summary>
        public void SetSeated(bool seated, bool asPilot)
        {
            capsule.enabled = !seated;

            if (IsOwner)
            {
                body.isKinematic = seated;
                if (!seated)
                    body.linearVelocity = Vector3.zero;
                if (interactor != null)
                    interactor.enabled = !seated;
                if (look != null)
                {
                    look.enabled = !seated || !asPilot;
                    if (seated && asPilot)
                        _head.localRotation = Quaternion.identity;
                }
            }

            enabled = !seated && IsOwner;
        }

        private void BindInput()
        {
            InputActionMap map = _inputAsset.FindActionMap("Player", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            lookAction = map.FindAction("Look", throwIfNotFound: true);
            jumpAction = map.FindAction("Jump", throwIfNotFound: true);
            sprintAction = map.FindAction("Sprint", throwIfNotFound: true);
            crouchAction = map.FindAction("Crouch", throwIfNotFound: true);
            map.Enable();
        }

        private void Update()
        {
            if (moveAction == null)
                return;

            pendingYaw += lookAction.ReadValue<Vector2>().x * _config.LookSensitivity;
            if (jumpAction.WasPressedThisFrame())
                jumpQueued = true;
            IsCrouched = crouchAction.IsPressed();

            UpdateCrouchShape();
        }

        private void FixedUpdate()
        {
            if (moveAction == null)
                return;

            gravity.Refresh();
            Vector3 up = gravity.Up;

            RotateBody(up);
            CheckGround(up);
            Move(up);
        }

        private void RotateBody(Vector3 up)
        {
            // Alignement de l'up local en slerp (jamais snap), puis yaw autour de l'up.
            Quaternion aligned = Quaternion.FromToRotation(transform.up, up) * body.rotation;
            float t = 1f - Mathf.Exp(-_config.UpAlignmentSharpness * Time.fixedDeltaTime);
            Quaternion smoothed = Quaternion.Slerp(body.rotation, aligned, t);
            smoothed *= Quaternion.Euler(0f, pendingYaw, 0f);
            pendingYaw = 0f;
            body.MoveRotation(smoothed);
        }

        private void CheckGround(Vector3 up)
        {
            float radius = capsule.radius * 0.9f;
            Vector3 origin = body.position + up * (radius + 0.05f);
            float distance = 0.05f + _config.GroundCheckDistance;
            IsGrounded = Physics.SphereCast(origin, radius, -up, out RaycastHit hit, distance, _groundMask, QueryTriggerInteraction.Ignore);
            groundNormal = IsGrounded ? hit.normal : up;
        }

        private void Move(Vector3 up)
        {
            float dt = Time.fixedDeltaTime;
            Vector2 move = moveAction.ReadValue<Vector2>();

            UpdateSprint(move, dt);
            float speed = IsCrouched ? _config.CrouchSpeed : IsSprinting ? _config.SprintSpeed : _config.WalkSpeed;

            Vector3 velocity = body.linearVelocity;

            if (IsGrounded)
            {
                // Au sol : contrôle de la vitesse complète le long de la pente (normale du sol),
                // pas d'intégration de gravité — un plaquage léger suit la courbure de la planète.
                Vector3 wishDir = transform.forward * move.y + transform.right * move.x;
                wishDir = Vector3.ProjectOnPlane(wishDir, groundNormal);
                if (wishDir.sqrMagnitude > 1e-4f)
                    wishDir = wishDir.normalized * Mathf.Clamp01(move.magnitude);

                velocity = Vector3.MoveTowards(velocity, wishDir * speed, _config.GroundAcceleration * dt);
                velocity -= up * (_config.GroundStickAcceleration * dt);

                if (jumpQueued)
                {
                    float g = Mathf.Max(gravity.CurrentAcceleration.magnitude, 0.01f);
                    velocity -= Vector3.Project(velocity, up);
                    velocity += up * Mathf.Sqrt(2f * g * _config.JumpHeight);
                }
            }
            else
            {
                // En l'air : contrôle réduit sur le plan tangent, gravité intégrée sur l'axe.
                Vector3 verticalVelocity = Vector3.Project(velocity, up);
                Vector3 planarVelocity = velocity - verticalVelocity;

                Vector3 wishDir = transform.forward * move.y + transform.right * move.x;
                wishDir = Vector3.ProjectOnPlane(wishDir, up);
                if (wishDir.sqrMagnitude > 1e-4f)
                    wishDir = wishDir.normalized * Mathf.Clamp01(move.magnitude);

                planarVelocity = Vector3.MoveTowards(planarVelocity, wishDir * speed, _config.AirAcceleration * dt);
                verticalVelocity += gravity.CurrentAcceleration * dt;
                velocity = planarVelocity + verticalVelocity;
            }

            jumpQueued = false;
            body.linearVelocity = velocity;
        }

        private void UpdateSprint(Vector2 move, float dt)
        {
            bool wantsSprint = sprintAction.IsPressed() && move.y > 0.1f && !IsCrouched && IsGrounded;

            if (sprintExhausted && stamina >= _config.StaminaMax * _config.SprintRecoveryThreshold)
                sprintExhausted = false;

            IsSprinting = wantsSprint && !sprintExhausted && stamina > 0f;

            if (IsSprinting)
            {
                stamina -= _config.StaminaDrainPerSecond * dt;
                lastSprintTime = Time.time;
                if (stamina <= 0f)
                {
                    stamina = 0f;
                    sprintExhausted = true;
                    IsSprinting = false;
                }
            }
            else if (Time.time - lastSprintTime >= _config.StaminaRegenDelay)
            {
                stamina = Mathf.Min(stamina + _config.StaminaRegenPerSecond * dt, _config.StaminaMax);
            }
        }

        private void UpdateCrouchShape()
        {
            float targetHeight = IsCrouched ? _config.CrouchHeight : _config.StandingHeight;
            float t = 1f - Mathf.Exp(-_config.CrouchLerpSharpness * Time.deltaTime);
            float height = Mathf.Lerp(capsule.height, targetHeight, t);
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);

            if (_head != null)
            {
                float headY = _config.HeadHeight - (_config.StandingHeight - height);
                Vector3 local = _head.localPosition;
                _head.localPosition = new Vector3(local.x, headY, local.z);
            }
        }
    }
}
