using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniMediator.Runtime;

namespace UniMediator.Tests
{

    [TestFixture]
    public class AsyncRequestTests : MediatorTestBase
    {
        public class AsyncPing : IAsyncRequest<string> { public string Message { get; set; } }
        public class AsyncPingHandler : IAsyncRequestHandler<AsyncPing, string>
        {
            public async UniTask<string> Handle(AsyncPing request, CancellationToken cancellationToken)
            {
                await UniTask.Delay(10, cancellationToken: cancellationToken);
                return $"Async Pong: {request.Message}";
            }

        }

        public class AsyncOneWay : IAsyncRequest { public string Data { get; set; } }
        public class AsyncOneWayHandler : IAsyncRequestHandler<AsyncOneWay>
        {
            public string ReceivedData { get; private set; }
            public async UniTask Handle(AsyncOneWay request, CancellationToken cancellationToken)
            {
                await UniTask.Delay(10, cancellationToken: cancellationToken);
                ReceivedData = request.Data;
            }

        }

        [Test]
        public async Task SendAsync_WithResponse_ReturnsExpectedValue()
        {
            Register<IAsyncRequestHandler<AsyncPing, string>>(new AsyncPingHandler());
            var response = await Mediator.SendAsync(new AsyncPing { Message = "Hello" });
            Assert.AreEqual("Async Pong: Hello", response);
        }

        [Test]
        public async Task SendAsync_WithoutResponse_CompletesSuccessfully()
        {
            var handler = new AsyncOneWayHandler();
            Register<IAsyncRequestHandler<AsyncOneWay>>(handler);
            await Mediator.SendAsync(new AsyncOneWay { Data = "Async Test" });
            Assert.AreEqual("Async Test", handler.ReceivedData);
        }

        [Test]
        public void SendAsync_WithCancellationToken_ThrowsOperationCanceledException()
        {
            Register<IAsyncRequestHandler<AsyncPing, string>>(new AsyncPingHandler());
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await Mediator.SendAsync(new AsyncPing { Message = "Hello" }, cts.Token));

        }

        [Test]
        public void SendAsync_ThrowsIfHandlerNotFound()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () => await Mediator.SendAsync(new AsyncPing()));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await Mediator.SendAsync(new AsyncOneWay()));
        }

        [Test]
        public void SendAsync_ThrowsIfRequestNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () => await Mediator.SendAsync((IAsyncRequest<string>)null));
            Assert.ThrowsAsync<ArgumentNullException>(async () => await Mediator.SendAsync((IAsyncRequest)null));
        }

        struct AsyncStructPing : IAsyncRequest<string> { public string Message; }
        class Handler : IAsyncRequestHandler<AsyncStructPing, string>
        {
            public async UniTask<string> Handle(AsyncStructPing req, CancellationToken ct)
            {
                await UniTask.Yield();
                return req.Message;
            }
        }

        [Test]
        public async Task SendAsync_StructRequest_ZeroAllocations()
        {
            Register<IAsyncRequestHandler<AsyncStructPing, string>>(new Handler());
            var request = new AsyncStructPing { Message = "Hi" };

            // Warm‑up
            await Mediator.SendAsync<string>(request);
            await Mediator.SendAsync<string>(request);

            // Force a full GC to establish a clean baseline
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();

            const int iterations = 100;
            for (int i = 0; i < iterations; i++)
            {
                await Mediator.SendAsync<string>(request);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            long allocated = after - before;

            // Output visible in the Test Runner window
            TestContext.WriteLine($"Allocated bytes over {iterations} async calls: {allocated}");
            TestContext.WriteLine($"Average per call: {(double)allocated / iterations:F2} bytes");

            Assert.AreEqual(0, allocated,
                $"Expected zero allocations, but {allocated} bytes were allocated across {iterations} calls.");
        }
    }
}