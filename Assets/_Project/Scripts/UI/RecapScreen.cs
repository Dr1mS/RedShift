using Redshift.Core;
using Redshift.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Redshift.UI
{
    /// <summary>
    /// Écran récap de fin de système (SPEC §3.2, P3-9) : s'affiche à la phase Saut.
    /// Issue du joueur (évadé / resté / perdu), quota atteint ou raté (−1 Cœur, stub P3),
    /// valeur déposée et durée de la run. Le retour méta (shop, système suivant) arrive en P5.
    /// </summary>
    public class RecapScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statsText;

        private PlayerHealth localHealth;
        private float showDelay = -1f;
        private float frozenRunTime = -1f;

        private void OnEnable() => GameEvents.PhaseChanged += OnPhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= OnPhaseChanged;

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Jump)
            {
                // Petit délai : le temps que l'évacuation répliquée arrive.
                showDelay = 0.75f;
            }
            else
            {
                showDelay = -1f;
                frozenRunTime = -1f;
                _panel.SetActive(false);
            }
        }

        private void Update()
        {
            if (showDelay > 0f)
            {
                showDelay -= Time.deltaTime;
                if (showDelay <= 0f)
                    _panel.SetActive(true);
            }
            if (_panel.activeSelf)
                Refresh(); // l'issue peut encore changer (onde qui rattrape un retardataire)
        }

        private void Refresh()
        {
            var director = GameDirector.Instance;
            if (director == null)
                return;
            if (localHealth == null)
            {
                foreach (PlayerHealth candidate in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
                    if (candidate.IsOwner)
                        localHealth = candidate;
                if (localHealth == null)
                    return;
            }
            if (frozenRunTime < 0f)
                frozenRunTime = director.ElapsedSystemTime;

            string title;
            Color titleColor;
            if (localHealth.IsEvacuated)
            {
                title = "SYSTÈME QUITTÉ";
                titleColor = new Color(0.6f, 0.9f, 0.6f);
            }
            else if (localHealth.IsDead)
            {
                title = "PERDU DANS LA SUPERNOVA";
                titleColor = new Color(1f, 0.3f, 0.25f);
            }
            else
            {
                title = "RESTÉ SUR PLACE — L'ONDE APPROCHE…";
                titleColor = new Color(1f, 0.6f, 0.2f);
            }
            _titleText.text = title;
            _titleText.color = titleColor;

            int minutes = Mathf.FloorToInt(frozenRunTime / 60f);
            int seconds = Mathf.FloorToInt(frozenRunTime % 60f);
            string quotaLine = director.IsQuotaMet
                ? $"Quota ATTEINT — {director.QuotaDeposited} / {director.QuotaTarget}"
                : $"Quota RATÉ — {director.QuotaDeposited} / {director.QuotaTarget}   (−1 Cœur d'intégrité)";
            _statsText.text = $"{quotaLine}\nDurée du système : {minutes:00}:{seconds:00}\n\nRelancer la partie pour une nouvelle expédition (boucle méta en P5).";
        }
    }
}
