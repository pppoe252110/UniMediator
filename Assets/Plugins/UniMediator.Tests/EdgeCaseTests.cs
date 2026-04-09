using NUnit.Framework;
using System;
using UniMediator.Runtime;

namespace UniMediator.Tests
{
    [TestFixture]
    public class EdgeCaseTests : MediatorTestBase
    {
        [Test]
        public void Resolver_ReturnsNull_ThrowsInvalidOperationException()
        {
            var nullMediator = new Mediator(_ => null);
            Assert.Throws<InvalidOperationException>(() => nullMediator.Send(new SyncRequestTests.Ping()));
        }
    }
}