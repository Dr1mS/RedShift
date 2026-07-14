using System;

namespace Redshift.Meta
{
    /// <summary>
    /// Comptabilité du quota d'un système : seul le loot déposé en soute compte (SPEC §3.2).
    /// L'excédent au-delà du quota se vendra en Crédits en phase FTL (P5).
    /// </summary>
    public class QuotaLedger
    {
        public int Target { get; }
        public int Deposited { get; private set; }

        public bool IsQuotaMet => Deposited >= Target;
        public int Remaining => Math.Max(0, Target - Deposited);
        public int Surplus => Math.Max(0, Deposited - Target);

        public QuotaLedger(int target)
        {
            Target = Math.Max(0, target);
        }

        /// <summary>Dépose une valeur et renvoie le total. Les valeurs nulles ou négatives sont ignorées.</summary>
        public int Deposit(int value)
        {
            if (value > 0)
                Deposited += value;
            return Deposited;
        }
    }
}
