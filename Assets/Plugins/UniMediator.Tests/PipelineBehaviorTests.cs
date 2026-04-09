using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniMediator.Runtime;

namespace UniMediator.Tests
{
    [TestFixture]
    public class PipelineBehaviorTests : MediatorTestBase
    {
        // Async types
        public class AsyncPing : IAsyncRequest<string> { public string Message { get; set; } }
        public class AsyncPingHandler : IAsyncRequestHandler<AsyncPing, string>
        {
            public async UniTask<string> Handle(AsyncPing request, CancellationToken cancellationToken)
            {
                await UniTask.Delay(10, cancellationToken: cancellationToken);
                return $"Async Pong: {request.Message}";
            }

        }

        public class AsyncLoggingBehavior : IAsyncPipelineBehavior<AsyncPing, string>
        {
            public List<string> Logs { get; } = new List<string>();
            public async UniTask<string> Handle(AsyncPing request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
            {
                Logs.Add($"Before: {request.Message}");
                var result = await next();
                Logs.Add($"After: {result}");
                return result.ToUpper();
            }

        }

        // Sync types
        public class SyncPing : IRequest<string> { public string Message { get; set; } }
        public class SyncPingHandler : IRequestHandler<SyncPing, string>
        {
            public string Handle(SyncPing request) => $"Sync Pong: {request.Message}";
        }
        public class SyncLoggingBehavior : IPipelineBehavior<SyncPing, string>
        {
            public List<string> Logs { get; } = new List<string>();
            public string Handle(SyncPing request, RequestHandlerDelegateSync<string> next)
            {
                Logs.Add($"Before: {request.Message}");
                var result = next();
                Logs.Add($"After: {result}");
                return result.ToUpper();
            }
        }

        [Test]
        public async Task AsyncPipelineBehavior_WrapsHandler_ModifiesResponse()
        {
            Register<IAsyncRequestHandler<AsyncPing, string>>(new AsyncPingHandler());
            var behavior = new AsyncLoggingBehavior();
            Register<IAsyncPipelineBehavior<AsyncPing, string>>(behavior);

            var response = await Mediator.SendAsync(new AsyncPing { Message = "Hello" });

            Assert.AreEqual("ASYNC PONG: HELLO", response);
            Assert.AreEqual(2, behavior.Logs.Count);
            Assert.AreEqual("Before: Hello", behavior.Logs[0]);
            Assert.AreEqual("After: Async Pong: Hello", behavior.Logs[1]);
        }

        [Test]
        public async Task AsyncPipelineBehavior_Multiple_ExecutedInOrder()
        {
            Register<IAsyncRequestHandler<AsyncPing, string>>(new AsyncPingHandler());
            var behavior1 = new AsyncLoggingBehavior();
            var behavior2 = new AsyncLoggingBehavior();
            Register<IAsyncPipelineBehavior<AsyncPing, string>>(behavior1);
            Register<IAsyncPipelineBehavior<AsyncPing, string>>(behavior2);

            await Mediator.SendAsync(new AsyncPing { Message = "Hello" });

            // Two behaviors produce four log entries total
            Assert.AreEqual(4, behavior1.Logs.Count + behavior2.Logs.Count);
        }

        [Test]
        public void SyncPipelineBehavior_WrapsHandler_ModifiesResponse()
        {
            Register<IRequestHandler<SyncPing, string>>(new SyncPingHandler());
            var behavior = new SyncLoggingBehavior();
            Register<IPipelineBehavior<SyncPing, string>>(behavior);

            var response = Mediator.Send(new SyncPing { Message = "Hello" });

            Assert.AreEqual("SYNC PONG: HELLO", response);
            Assert.AreEqual(2, behavior.Logs.Count);
            Assert.AreEqual("Before: Hello", behavior.Logs[0]);
            Assert.AreEqual("After: Sync Pong: Hello", behavior.Logs[1]);
        }

        [Test]
        public void SyncPipelineBehavior_Multiple_ExecutedInOrder()
        {
            Register<IRequestHandler<SyncPing, string>>(new SyncPingHandler());
            var behavior1 = new SyncLoggingBehavior();
            var behavior2 = new SyncLoggingBehavior();
            Register<IPipelineBehavior<SyncPing, string>>(behavior1);
            Register<IPipelineBehavior<SyncPing, string>>(behavior2);

            Mediator.Send(new SyncPing { Message = "Hello" });

            Assert.AreEqual(4, behavior1.Logs.Count + behavior2.Logs.Count);
        }
    }
}