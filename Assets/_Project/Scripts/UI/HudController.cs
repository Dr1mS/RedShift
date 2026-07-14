using Redshift.Core;
using Redshift.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Redshift.UI
{
    /// <summary>
    /// HUD minimal (P3-8) : phase + timer, quota, santé, chaleur laser, prompt
    /// d'interaction, main/slots, bandeau de mort. Se lie au joueur local à son
    /// spawn ; lit l'état répliqué (GameDirector) et les composants du propriétaire.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private Text _phaseText;
        [SerializeField] private Text _quotaText;
        [SerializeField] private Text _promptText;
        [SerializeField] private Text _handText;
        [SerializeField] private Text _slotsText;
        [SerializeField] private Image _healthFill;
        [SerializeField] private Text _healthText;
        [SerializeField] private GameObject _heatGroup;
        [SerializeField] private Image _heatFill;
        [SerializeField] private GameObject _deathBanner;

        private static readonly Color PhaseCalm = Color.white;
        private static readonly Color PhaseCritical = new(1f, 0.55f, 0.2f);
        private static readonly Color PhaseCollapse = new(1f, 0.25f, 0.2f);
        private static readonly Color HeatNormal = new(1f, 0.6f, 0.2f);
        private static readonly Color HeatLocked = new(1f, 0.2f, 0.1f);

        private PlayerMotor motor;
        private PlayerHealth health;
        private PlayerInventory inventory;
        private PlayerInteractor interactor;
        private MiningLaser laser;
        private float bindTimer;
        private readonly System.Text.StringBuilder slotsBuilder = new();

        private void Update()
        {
            UpdateDirectorPanel();
            if (TryBindLocalPlayer())
                UpdatePlayerPanel();
        }

        private void UpdateDirectorPanel()
        {
            var director = GameDirector.Instance;
            if (director == null)
            {
                _phaseText.text = string.Empty;
                _quotaText.text = string.Empty;
                return;
            }

            _phaseText.text = PhaseLabel(director.Phase, director.PhaseRemaining);
            _phaseText.color = director.Phase switch
            {
                GamePhase.Critical => PhaseCritical,
                GamePhase.Collapse => PhaseCollapse,
                _ => PhaseCalm,
            };
            _quotaText.text = $"QUOTA {director.QuotaDeposited} / {director.QuotaTarget}";
        }

        private static string PhaseLabel(GamePhase phase, float remaining)
        {
            string name = phase switch
            {
                GamePhase.Ftl => "FTL",
                GamePhase.Arrival => "ARRIVÉE",
                GamePhase.Exploration => "EXPLORATION",
                GamePhase.Critical => "CRITIQUE",
                GamePhase.Collapse => "EFFONDREMENT",
                _ => "SAUT",
            };
            if (float.IsPositiveInfinity(remaining))
                return name;
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            return $"{name}  {minutes:00}:{seconds:00}";
        }

        private bool TryBindLocalPlayer()
        {
            if (motor != null)
                return true;
            bindTimer -= Time.deltaTime;
            if (bindTimer > 0f)
                return false;
            bindTimer = 0.5f;

            foreach (PlayerMotor candidate in FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
            {
                if (!candidate.IsOwner)
                    continue;
                motor = candidate;
                health = candidate.GetComponent<PlayerHealth>();
                inventory = candidate.GetComponent<PlayerInventory>();
                laser = candidate.GetComponent<MiningLaser>();
                interactor = candidate.GetComponentInChildren<PlayerInteractor>(true);
                return true;
            }
            return false;
        }

        private void UpdatePlayerPanel()
        {
            bool dead = health != null && health.IsDead;
            if (_deathBanner.activeSelf != dead)
                _deathBanner.SetActive(dead);

            float hp = health != null ? health.HealthNormalized : 1f;
            _healthFill.fillAmount = hp;
            _healthText.text = $"SANTÉ {Mathf.RoundToInt(hp * 100f)}";

            bool showHeat = !dead && laser != null && laser.Heat01 > 0.001f;
            if (_heatGroup.activeSelf != showHeat)
                _heatGroup.SetActive(showHeat);
            if (showHeat)
            {
                _heatFill.fillAmount = laser.Heat01;
                _heatFill.color = laser.Overheated ? HeatLocked : HeatNormal;
            }

            IInteractable target = !dead && interactor != null && interactor.enabled ? interactor.CurrentTarget : null;
            _promptText.text = target != null && target.CanInteract(interactor) ? $"[E] {target.Prompt}" : string.Empty;

            if (dead || inventory == null)
            {
                _handText.text = string.Empty;
                _slotsText.text = string.Empty;
                return;
            }

            WorldItem hand = inventory.HandItem;
            _handText.text = hand != null ? $"Main : {hand.Def.DisplayName}   [G] lâcher   [1-4] ranger" : string.Empty;

            slotsBuilder.Clear();
            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                WorldItem slot = inventory.GetSlot(i);
                slotsBuilder.Append(i + 1).Append(' ').Append(slot != null ? slot.Def.DisplayName : "—");
                if (i < PlayerInventory.SlotCount - 1)
                    slotsBuilder.Append("      ");
            }
            _slotsText.text = slotsBuilder.ToString();
        }
    }
}
