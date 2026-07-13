using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Suit le champ de gravité dominant pour cet objet (résolution priorité + hystérésis)
    /// et applique l'accélération au Rigidbody attaché — sauf si un autre système
    /// (ex : PlayerMotor) prend la main via <see cref="ApplyToBody"/> = false.
    /// Pas de NetworkBehaviour : chaque pair résout localement le même résultat
    /// (sources statiques) ; la position, elle, est déjà synchronisée par ailleurs.
    /// </summary>
    [DisallowMultipleComponent]
    public class GravityReceiver : MonoBehaviour
    {
        [SerializeField, Tooltip("Hystérésis de transition entre champs (fraction du rayon d'influence). SPEC §4.2.")]
        private float _hysteresis = 0.1f;

        [SerializeField, Tooltip("Si vrai, applique la gravité au Rigidbody en FixedUpdate. Mettre à faux quand un contrôleur gère lui-même la force.")]
        private bool _applyToBody = true;

        private Rigidbody body;
        private int currentFieldId = GravityResolver.NoField;

        public bool ApplyToBody { get => _applyToBody; set => _applyToBody = value; }

        /// <summary>Champ courant, ou null en micro-gravité.</summary>
        public GravitySource CurrentSource { get; private set; }

        /// <summary>Accélération de gravité courante (Vector3.zero en micro-gravité).</summary>
        public Vector3 CurrentAcceleration { get; private set; }

        /// <summary>« Haut » local (opposé à la gravité). Vaut transform.up en micro-gravité.</summary>
        public Vector3 Up => CurrentAcceleration.sqrMagnitude > 1e-6f ? -CurrentAcceleration.normalized : transform.up;

        public bool InGravity => CurrentSource != null;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (body != null)
                body.useGravity = false;
        }

        private void FixedUpdate()
        {
            Refresh();
            // IsSleeping : ne jamais réveiller un rigidbody endormi (loot endormi par défaut, piège connu).
            if (_applyToBody && body != null && !body.isKinematic && !body.IsSleeping() && InGravity)
                body.AddForce(CurrentAcceleration, ForceMode.Acceleration);
        }

        /// <summary>Résout le champ dominant à la position actuelle (appelable hors FixedUpdate, ex : tests).</summary>
        public void Refresh()
        {
            var fields = GravitySource.Fields;
            currentFieldId = GravityResolver.Resolve(fields, transform.position, currentFieldId, _hysteresis);

            if (currentFieldId == GravityResolver.NoField)
            {
                CurrentSource = null;
                CurrentAcceleration = Vector3.zero;
                return;
            }

            CurrentSource = GravitySource.FindById(currentFieldId);
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].Id == currentFieldId)
                {
                    CurrentAcceleration = fields[i].AccelerationAt(transform.position);
                    break;
                }
            }
        }
    }
}
