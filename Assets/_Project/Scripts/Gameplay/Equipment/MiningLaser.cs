using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Redshift.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Pioche laser T1 (SPEC §4.1/§4.5). La chaleur est simulée chez le propriétaire ;
    /// les dégâts sont accumulés puis validés serveur par lots (pas de RPC par frame).
    /// Le minage fait du bruit et de la lumière : état de tir répliqué (faisceau visible
    /// par les autres) + GameEvents.NoiseEmitted côté host (aggro Nuée en P4).
    /// </summary>
    public class MiningLaser : NetworkBehaviour
    {
        [SerializeField] private MiningLaserDef _def;
        [SerializeField, Tooltip("Origine du rayon : la tête (visée).")]
        private Transform _head;
        [SerializeField, Tooltip("Gèle le tir quand le joueur est assis/gelé.")]
        private PlayerMotor _motor;
        [SerializeField, Tooltip("Source du pitch de tête (owner) — répliqué vers les pairs distants.")]
        private PlayerLook _look;
        [SerializeField, Tooltip("Faisceau optionnel (2 points, monde).")]
        private LineRenderer _beam;
        [SerializeField] private LayerMask _mask = ~0;
        [SerializeField] private InputActionAsset _inputAsset;
        [Header("Réplication du pitch tête (config technique)")]
        [SerializeField, Tooltip("Cadence d'envoi du pitch owner→serveur (Hz).")]
        private float _pitchSendRate = 12f;
        [SerializeField, Tooltip("Seuil d'envoi (degrés) : pas de RPC sous cette variation.")]
        private float _pitchSendThreshold = 0.5f;
        [SerializeField, Tooltip("Netteté du slerp de lissage du pitch distant (1/s).")]
        private float _pitchLerpSharpness = 18f;

        private readonly SyncVar<bool> _firing = new();
        // Pitch de tête (degrés) : posé par le serveur via ServerRpc, lissé chez les pairs distants.
        private readonly SyncVar<float> _headPitch = new();

        private InputAction mineAction;
        private LaserHeatModel heat;
        private OreVein pendingVein;
        private float pendingDamage;
        private float sendTimer;
        private float pitchSendTimer;
        private float lastSentPitch = float.NaN;
        private float remotePitch;

        public float Heat01 => heat?.Heat01 ?? 0f;
        public bool Overheated => heat?.Overheated ?? false;
        public bool IsFiring => _firing.Value;

        private void Awake()
        {
            _firing.OnChange += (_, next, _) => UpdateBeamVisual(next);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return;
            heat = new LaserHeatModel(_def.SecondsToOverheat, _def.SecondsToCool, _def.ReArmThreshold);
            mineAction = _inputAsset.FindActionMap("Player", throwIfNotFound: true).FindAction("Mine", throwIfNotFound: true);
        }

        private void Update()
        {
            if (IsOwner)
            {
                OwnerUpdate();
                return;
            }

            // Pair distant (host inclus pour les autres joueurs) : PlayerLook est désactivé ici,
            // on pilote donc nous-mêmes le pitch de la tête depuis la SyncVar, lissé.
            ApplyRemotePitch();
            if (_firing.Value)
                UpdateBeamVisual(true); // faisceau distant suit la tête (désormais orientée).
        }

        private void OwnerUpdate()
        {
            SendPitch();

            if (mineAction == null)
                return;

            bool wantsFire = mineAction.IsPressed() && _motor != null && _motor.enabled;
            bool firing = heat.Tick(wantsFire, Time.deltaTime);

            if (firing != _firing.Value)
                SetFiringServerRpc(firing);

            if (firing)
                MineTick(Time.deltaTime);
            else
                FlushDamage();

            if (_firing.Value)
                UpdateBeamVisual(true); // suit la visée du propriétaire chaque frame
        }

        /// <summary>Owner : diffuse le pitch de tête à cadence limitée (config technique), sur seuil.</summary>
        private void SendPitch()
        {
            if (_look == null)
                return;

            pitchSendTimer += Time.deltaTime;
            float interval = _pitchSendRate > 0f ? 1f / _pitchSendRate : 0f;
            if (pitchSendTimer < interval)
                return;

            float pitch = _look.Pitch;
            if (!float.IsNaN(lastSentPitch) && Mathf.Abs(pitch - lastSentPitch) < _pitchSendThreshold)
                return;

            pitchSendTimer = 0f;
            lastSentPitch = pitch;
            SetPitchServerRpc(pitch);
        }

        /// <summary>Pair distant : lisse le pitch reçu et l'écrit sur la tête (jamais chez l'owner).</summary>
        private void ApplyRemotePitch()
        {
            if (_head == null)
                return;

            float t = 1f - Mathf.Exp(-_pitchLerpSharpness * Time.deltaTime);
            remotePitch = Mathf.LerpAngle(remotePitch, _headPitch.Value, t);
            _head.localRotation = Quaternion.Euler(remotePitch, 0f, 0f);
        }

        private void MineTick(float dt)
        {
            bool hasHit = Physics.Raycast(_head.position, _head.forward, out RaycastHit hit, _def.Range, _mask, QueryTriggerInteraction.Ignore);
            OreVein vein = hasHit ? hit.collider.GetComponentInParent<OreVein>() : null;

            // Changement de cible : on solde d'abord les dégâts dus à l'ancienne.
            if (vein != pendingVein)
                FlushDamage();
            pendingVein = vein;

            if (vein != null && !vein.Depleted)
                pendingDamage += _def.DamagePerSecond * dt;

            sendTimer += dt;
            if (sendTimer >= _def.DamageSendInterval)
                FlushDamage();
        }

        private void FlushDamage()
        {
            sendTimer = 0f;
            if (pendingVein == null || pendingDamage <= 0f)
            {
                pendingDamage = 0f;
                return;
            }
            MineServerRpc(pendingVein, pendingDamage);
            pendingDamage = 0f;
        }

        [ServerRpc]
        private void SetFiringServerRpc(bool firing) => _firing.Value = firing;

        // Canal unreliable : le pitch est fréquent et tolère la perte (interpolé à la réception).
        [ServerRpc]
        private void SetPitchServerRpc(float pitch, Channel channel = Channel.Unreliable) => _headPitch.Value = pitch;

        [ServerRpc]
        private void MineServerRpc(OreVein vein, float damage)
        {
            if (vein == null || damage <= 0f)
                return;
            // Borne anti-aberration : pas plus que le débit max d'un lot (co-op, SPEC §5).
            float maxBatch = _def.DamagePerSecond * _def.DamageSendInterval * 2f;
            vein.ApplyMiningDamage(Mathf.Min(damage, maxBatch), vein.transform.position);
            GameEvents.RaiseNoiseEmitted(vein.transform.position, _def.NoiseLoudness);
        }

        private void UpdateBeamVisual(bool firing)
        {
            if (_beam == null)
                return;
            _beam.enabled = firing;
            if (!firing)
                return;

            // Le pitch de tête est répliqué (ApplyRemotePitch) : le faisceau distant suit la visée.
            Vector3 origin = _head.position;
            Vector3 end = origin + _head.forward * _def.Range;
            if (IsOwner && Physics.Raycast(origin, _head.forward, out RaycastHit hit, _def.Range, _mask, QueryTriggerInteraction.Ignore))
                end = hit.point;
            _beam.SetPosition(0, origin);
            _beam.SetPosition(1, end);
        }
    }
}
