using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Marque un objet détectable par le scanner (items, gisements, entrées de ruines — SPEC §4.1).</summary>
    public class ScannableTarget : MonoBehaviour
    {
        [SerializeField] private string _displayName = "?";

        public string DisplayName => _displayName;

        public void SetDisplayName(string value) => _displayName = value;
    }
}
