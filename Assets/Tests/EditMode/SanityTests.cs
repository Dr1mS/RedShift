using NUnit.Framework;

namespace Redshift.Tests.EditMode
{
    /// <summary>Test trivial du gate G0 : prouve que la chaîne de tests EditMode tourne.</summary>
    public class SanityTests
    {
        [Test]
        public void TestPipeline_IsOperational()
        {
            Assert.That(2 + 2, Is.EqualTo(4));
        }
    }
}
