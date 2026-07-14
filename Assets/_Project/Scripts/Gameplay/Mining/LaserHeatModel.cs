using System;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Chaleur du laser de minage (SPEC §4.5) : surchauffe = pause forcée jusqu'au
    /// seuil de réarmement. Logique pure, testable EditMode.
    /// </summary>
    public class LaserHeatModel
    {
        private readonly float heatPerSecond;
        private readonly float coolPerSecond;
        private readonly float reArmThreshold;

        public float Heat01 { get; private set; }
        public bool Overheated { get; private set; }
        public bool CanFire => !Overheated;

        public LaserHeatModel(float secondsToOverheat, float secondsToCool, float reArmThreshold)
        {
            heatPerSecond = 1f / Math.Max(0.01f, secondsToOverheat);
            coolPerSecond = 1f / Math.Max(0.01f, secondsToCool);
            this.reArmThreshold = Math.Clamp(reArmThreshold, 0f, 0.99f);
        }

        /// <summary>Avance la simulation ; renvoie vrai si le faisceau a tiré pendant ce pas.</summary>
        public bool Tick(bool wantsFire, float dt)
        {
            bool firing = wantsFire && CanFire;
            if (firing)
            {
                Heat01 = Math.Min(1f, Heat01 + heatPerSecond * dt);
                if (Heat01 >= 1f)
                    Overheated = true;
            }
            else
            {
                Heat01 = Math.Max(0f, Heat01 - coolPerSecond * dt);
                if (Overheated && Heat01 <= reArmThreshold)
                    Overheated = false;
            }
            return firing;
        }
    }
}
