using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>Tunables de la lampe frontale T1 (SPEC §4.1).</summary>
    [CreateAssetMenu(menuName = "Redshift/Data/Lamp", fileName = "SO_Equip_Lamp")]
    public class LampDef : ScriptableObject
    {
        [Tooltip("Autonomie de la batterie (s). Recharge à l'Arche (post-P1).")]
        public float BatteryDuration = 300f;
        public float Range = 25f;
        public float SpotAngle = 65f;
        public float Intensity = 3f;
    }
}
