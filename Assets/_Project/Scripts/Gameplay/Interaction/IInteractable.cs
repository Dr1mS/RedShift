namespace Redshift.Gameplay
{
    /// <summary>Tout ce que le joueur peut viser + activer (items, portes, sièges, terminaux…).</summary>
    public interface IInteractable
    {
        /// <summary>Texte du prompt (ex : « Ramasser Plaque de ferraille »).</summary>
        string Prompt { get; }

        bool CanInteract(PlayerInteractor interactor);

        void Interact(PlayerInteractor interactor);
    }
}
