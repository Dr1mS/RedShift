using System;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Rendement d'un filon (SPEC §4.5) : les unités de minerai sortent proportionnellement
    /// aux dégâts infligés, sans double comptage. Logique pure, testable EditMode.
    /// </summary>
    public class VeinYieldTracker
    {
        private readonly float totalHp;
        private readonly int totalUnits;
        private float damage;

        public int UnitsSpawned { get; private set; }
        public bool Depleted => damage >= totalHp;
        public float RemainingHp => Math.Max(0f, totalHp - damage);

        public VeinYieldTracker(float totalHp, int totalUnits)
        {
            this.totalHp = Math.Max(0.01f, totalHp);
            this.totalUnits = Math.Max(0, totalUnits);
        }

        /// <summary>Applique des dégâts et renvoie le nombre d'unités à faire apparaître maintenant.</summary>
        public int ApplyDamage(float amount)
        {
            damage = Math.Min(totalHp, damage + Math.Max(0f, amount));
            // Epsilon : garantit totalUnits exactement quand damage == totalHp malgré le float.
            int owed = (int)Math.Floor(damage / totalHp * totalUnits + 1e-4f);
            int toSpawn = Math.Max(0, Math.Min(totalUnits, owed) - UnitsSpawned);
            UnitsSpawned += toSpawn;
            return toSpawn;
        }
    }
}
