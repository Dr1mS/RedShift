using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Porte-portail d'une poche de ruine (SPEC D6, §6.3). Autorité : le mouvement joueur
    /// est owner-authoritative, donc le téléport s'exécute chez le propriétaire — aucun
    /// état partagé (pas de NetworkBehaviour nécessaire). L'objet tenu suit la main
    /// localement chez tous les pairs (WorldItem.LateUpdate), kinématique : jamais de
    /// physique active pendant le téléport. Le rigidbody joueur est gelé le temps du saut.
    /// </summary>
    public class PortalLink : MonoBehaviour, IInteractable
    {
        [SerializeField] private PortalDef _def;
        [SerializeField, Tooltip("Portail de destination.")]
        private PortalLink _target;
        [SerializeField, Tooltip("Point d'arrivée devant CE portail (utilisé par le portail opposé).")]
        private Transform _arrivalAnchor;
        [SerializeField] private string _prompt = "Franchir le portail";

        // Partagé entre tous les portails : anti aller-retour immédiat côté local.
        private static float lastLocalUseTime = float.NegativeInfinity;

        public Transform ArrivalAnchor => _arrivalAnchor;
        public string Prompt => _prompt;

        public bool CanInteract(PlayerInteractor interactor)
            => _target != null && Time.time - lastLocalUseTime >= _def.Cooldown;

        public void Interact(PlayerInteractor interactor)
        {
            var motor = interactor.GetComponentInParent<PlayerMotor>();
            if (motor == null)
                return;

            lastLocalUseTime = Time.time;
            Teleport(motor, _target.ArrivalAnchor);
        }

        /// <summary>Téléport owner-side : physique gelée pendant le déplacement, gravité re-résolue.</summary>
        private static void Teleport(PlayerMotor motor, Transform anchor)
        {
            var body = motor.GetComponent<Rigidbody>();
            bool wasKinematic = body.isKinematic;

            body.isKinematic = true;
            motor.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            body.position = anchor.position;
            body.rotation = anchor.rotation;
            body.isKinematic = wasKinematic;
            if (!wasKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            var gravity = motor.GetComponent<GravityReceiver>();
            if (gravity != null)
                gravity.Refresh();
        }
    }
}
