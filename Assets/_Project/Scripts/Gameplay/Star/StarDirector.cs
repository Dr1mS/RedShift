using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Directeur visuel de l'étoile (SPEC §4.10, §7) : teinte + échelle du placeholder et
    /// lumière globale pilotées par l'instabilité répliquée du GameDirector. Purement local
    /// (pas un objet physique) — l'onde de choc est le seul collider létal.
    /// </summary>
    public class StarDirector : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private StarDef _def;
        [SerializeField, Tooltip("Sphère visuelle de l'étoile (Star_Visual).")]
        private Renderer _starRenderer;
        [SerializeField, Tooltip("Lumière directionnelle du système (Star_Placeholder).")]
        private Light _systemLight;

        private Vector3 initialScale;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            initialScale = _starRenderer.transform.localScale;
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            var director = GameDirector.Instance;
            if (director == null)
                return;

            float instability = director.Instability01;

            // Gonflement en instabilité² : lent en Exploration, visible en Critique (SPEC §3.2).
            float swell = Mathf.Lerp(1f, _def.CollapseScaleMultiplier, instability * instability);
            _starRenderer.transform.localScale = initialScale * swell;

            block.SetColor(BaseColorId, Color.Lerp(_def.StarStableColor, _def.StarCriticalColor, instability));
            _starRenderer.SetPropertyBlock(block);

            if (_systemLight != null)
            {
                _systemLight.color = Color.Lerp(_def.LightStableColor, _def.LightCriticalColor, instability);
                _systemLight.intensity = Mathf.Lerp(_def.LightStableIntensity, _def.LightCriticalIntensity, instability);
            }
        }
    }
}
