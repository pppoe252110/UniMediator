using NUnit.Framework;
using System;
using UniMediator.Runtime;

namespace UniMediator.Tests
{
    [TestFixture]
    public class SyncRequestTests : MediatorTestBase
    {
        public class Ping : IRequest<string> { public string Message { get; set; } }
        public class PingHandler : IRequestHandler<Ping, string>
        {
            public string Handle(Ping request) => $"Pong: {request.Message}";
        }

        public class OneWay : IRequest { public string Data { get; set; } }
        public class OneWayHandler : IRequestHandler<OneWay>
        {
            public string ReceivedData { get; private set; }
            public void Handle(OneWay request) => ReceivedData = request.Data;
        }

        [Test]
        public void Send_WithResponse_ReturnsExpectedValue()
        {
            Register<IRequestHandler<Ping, string>>(new PingHandler());
            var response = Mediator.Send(new Ping { Message = "Hello" });
            Assert.AreEqual("Pong: Hello", response);
        }

        [Test]
        public void Send_WithoutResponse_InvokesHandler()
        {
            var handler = new OneWayHandler();
            Register<IRequestHandler<OneWay>>(handler);
            Mediator.Send(new OneWay { Data = "Test" });
            Assert.AreEqual("Test", handler.ReceivedData);
        }

        [Test]
        public void Send_ThrowsIfHandlerNotFound()
        {
            Assert.Throws<InvalidOperationException>(() => Mediator.Send(new Ping()));
        }

        [Test]
        public void Send_ThrowsIfRequestNull()
        {
            Assert.Throws<ArgumentNullException>(() => Mediator.Send((IRequest<string>)null));
            Assert.Throws<ArgumentNullException>(() => Mediator.Send((IRequest)null));
        }

        [Test]
        public void RepeatedSend_UsesCachedInvoker()
        {
            Register<IRequestHandler<Ping, string>>(new PingHandler());
            var response1 = Mediator.Send(new Ping { Message = "First" });
            var response2 = Mediator.Send(new Ping { Message = "Second" });
            Assert.AreEqual("Pong: First", response1);
            Assert.AreEqual("Pong: Second", response2);
        }

        struct StructPing : IRequest<string>
        {
            public string Message;
        }

        class StructPingHandler : IRequestHandler<StructPing, string>
        {
            public string Handle(StructPing request) => $"Pong: {request.Message}";
        }

        [Test]
        public void Send_StructRequest_ManualAllocationCheck()
        {

            Register<IRequestHandler<StructPing, string>>(new StructPingHandler());
            var request = new StructPing { Message = "Hello" };

            // Warm‑up
            Mediator.Send(request);
            Mediator.Send(request);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            const int iterations = 1000;
            for (int i = 0; i < iterations; i++)
            {
                Mediator.Send(request);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            long totalAllocated = after - before;
            double perCall = totalAllocated / (double)iterations;

            TestContext.WriteLine($"Total allocated: {totalAllocated} bytes over {iterations} calls");
            TestContext.WriteLine($"Per call: {perCall:F2} bytes");

            Assert.AreEqual(0, totalAllocated, $"Expected 0 allocations, got {totalAllocated} bytes.");
        }
    }
}