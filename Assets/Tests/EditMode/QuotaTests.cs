using NUnit.Framework;
using Redshift.Meta;

namespace Redshift.Tests.EditMode
{
    /// <summary>Économie du quota (SPEC §4.6) : quota(n) = base × growth^n + comptabilité des dépôts.</summary>
    public class QuotaTests
    {
        [Test]
        public void QuotaFor_FirstSystem_IsBase()
            => Assert.That(QuotaRules.QuotaFor(400, 1.35f, 0), Is.EqualTo(400));

        [Test]
        public void QuotaFor_GrowsGeometrically()
        {
            Assert.That(QuotaRules.QuotaFor(400, 1.35f, 1), Is.EqualTo(540));
            Assert.That(QuotaRules.QuotaFor(400, 1.35f, 2), Is.EqualTo(729));
        }

        [Test]
        public void QuotaFor_NegativeIndex_ClampsToBase()
            => Assert.That(QuotaRules.QuotaFor(400, 1.35f, -3), Is.EqualTo(400));

        [Test]
        public void Ledger_StartsEmpty()
        {
            var ledger = new QuotaLedger(100);
            Assert.That(ledger.Deposited, Is.EqualTo(0));
            Assert.That(ledger.IsQuotaMet, Is.False);
            Assert.That(ledger.Remaining, Is.EqualTo(100));
            Assert.That(ledger.Surplus, Is.EqualTo(0));
        }

        [Test]
        public void Ledger_Deposits_Accumulate()
        {
            var ledger = new QuotaLedger(100);
            ledger.Deposit(60);
            Assert.That(ledger.Deposited, Is.EqualTo(60));
            Assert.That(ledger.IsQuotaMet, Is.False);
            Assert.That(ledger.Remaining, Is.EqualTo(40));
        }

        [Test]
        public void Ledger_MetAtExactTarget()
        {
            var ledger = new QuotaLedger(100);
            ledger.Deposit(60);
            ledger.Deposit(40);
            Assert.That(ledger.IsQuotaMet, Is.True);
            Assert.That(ledger.Remaining, Is.EqualTo(0));
            Assert.That(ledger.Surplus, Is.EqualTo(0));
        }

        [Test]
        public void Ledger_SurplusBeyondTarget()
        {
            var ledger = new QuotaLedger(100);
            ledger.Deposit(125);
            Assert.That(ledger.Surplus, Is.EqualTo(25), "l'excédent se vendra en Crédits (P5)");
        }

        [Test]
        public void Ledger_IgnoresNonPositiveDeposits()
        {
            var ledger = new QuotaLedger(100);
            ledger.Deposit(0);
            ledger.Deposit(-50);
            Assert.That(ledger.Deposited, Is.EqualTo(0));
        }

        [Test]
        public void Ledger_ZeroTarget_IsImmediatelyMet()
            => Assert.That(new QuotaLedger(0).IsQuotaMet, Is.True);
    }
}
