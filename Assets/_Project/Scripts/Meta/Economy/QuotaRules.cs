using System;

namespace Redshift.Meta
{
    /// <summary>Règles d'économie pures (SPEC §4.6) : quota(n) = base × growth^n.</summary>
    public static class QuotaRules
    {
        /// <summary>Quota en valeur pour le n-ième système de l'expédition (n ≥ 0, clampe en dessous).</summary>
        public static int QuotaFor(int baseQuota, float growth, int systemIndex)
        {
            int n = Math.Max(0, systemIndex);
            return (int)Math.Round(baseQuota * Math.Pow(growth, n));
        }
    }
}
