#if UNIMEDIATOR_VCONTAINER_INTEGRATION
using System.Collections;
using System.Collections.Generic;
using System.Threading;
#if UNIMEDIATOR_UNITASK_INTEGRATION
using Cysharp.Threading.Tasks;
#endif
using NUnit.Framework;
using UniMediator.Runtime;
using UnityEngine.TestTools;
using VContainer;

namespace UniMediator.Examples.Tests
{
    [TestFixture]
    public class VContainerIntegrationTests
    {
        private IObjectResolver _container;

        // ============= Test Handlers & Requests =============
        public class TestSyncRequest : IRequest<string> { public string Input { get; set; } }
        public class TestSyncHandler : IRequestHandler<TestSyncRequest, string>
        {
            public string Handle(TestSyncRequest request) => $"Sync: {request.Input}";
        }

#if UNIMEDIATOR_UNITASK_INTEGRATION
        public class TestAsyncRequest : IAsyncRequest<int> { public int Value { get; set; } }

        public class TestAsyncHandler : IAsyncRequestHandler<TestAsyncRequest, int>
        {
            public async UniTask<int> Handle(TestAsyncRequest request, CancellationToken token)
            {
                await UniTask.Delay(10, cancellationToken: token);
                return request.Value * 2;
            }
        }
        public class TestNotification : INotification { public string Message { get; set; } }
        public class TestNotificationHandler : IAsyncNotificationHandler<TestNotification>
        {
            public List<string> Received { get; } = new();
            public UniTask HandleAsync(TestNotification notification, CancellationToken token)
            {
                Received.Add(notification.Message);
                return UniTask.CompletedTask;
            }
        }
#endif

        // Sync pipeline behavior (registered only when needed)
        public class UpperCaseBehavior : IPipelineBehavior<TestSyncRequest, string>
        {
            public string Handle(TestSyncRequest request, RequestHandlerDelegateSync<string> next)
                => next().ToUpperInvariant();
        }

#if UNIMEDIATOR_UNITASK_INTEGRATION
        // Async pipeline behavior
        public class LoggingAsyncBehavior : IAsyncPipelineBehavior<TestAsyncRequest, int>
        {
            public List<string> Logs { get; } = new();
            public async UniTask<int> Handle(TestAsyncRequest request, RequestHandlerDelegate<int> next, CancellationToken token)
            {
                Logs.Add($"Before: {request.Value}");
                var result = await next();
                Logs.Add($"After: {result}");
                return result;
            }
        }
#endif
        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();

            // Core handlers
            builder.Register<TestSyncHandler>(Lifetime.Transient)
                   .As<IRequestHandler<TestSyncRequest, string>>();
#if UNIMEDIATOR_UNITASK_INTEGRATION

            builder.Register<TestAsyncHandler>(Lifetime.Transient)
                   .As<IAsyncRequestHandler<TestAsyncRequest, int>>();
            builder.Register<TestNotificationHandler>(Lifetime.Singleton)
                   .As<IAsyncNotificationHandler<TestNotification>>();
#endif
            // Pipeline behaviors (commented out by default to keep sync test clean)
            // Uncomment for pipeline tests
            // builder.Register<UpperCaseBehavior>(Lifetime.Transient)
            //        .As<IPipelineBehavior<TestSyncRequest, string>>();
            // builder.Register<LoggingAsyncBehavior>(Lifetime.Transient)
            //        .As<IAsyncPipelineBehavior<TestAsyncRequest, int>>();

            // Mediator registration
            builder.Register<IMediator>(resolver => new Mediator(
                resolver: type => resolver.Resolve(type),
                multiResolver: type =>
                {
                    var enumerableType = typeof(IEnumerable<>).MakeGenericType(type);
                    return (IEnumerable<object>)resolver.Resolve(enumerableType);
                }
            ), Lifetime.Singleton);

            _container = builder.Build();
        }

        [Test]
        public void Mediator_IsResolvable()
        {
            var mediator = _container.Resolve<IMediator>();
            Assert.That(mediator, Is.Not.Null);
        }

        [Test]
        public void Send_SyncRequest_ReturnsExpectedResponse()
        {
            var mediator = _container.Resolve<IMediator>();
            var request = new TestSyncRequest { Input = "hello" };
            string result = mediator.Send(request);
            Assert.That(result, Is.EqualTo("Sync: hello"));
        }

#if UNIMEDIATOR_UNITASK_INTEGRATION
        [UnityTest]
        public IEnumerator Send_AsyncRequest_ReturnsExpectedResponse()
        {
            var mediator = _container.Resolve<IMediator>();
            var request = new TestAsyncRequest { Value = 5 };

            int result = 0;
            yield return mediator.SendAsync(request).ToCoroutine(r => result = r);

            Assert.That(result, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator Publish_Notification_InvokesHandler()
        {
            var mediator = _container.Resolve<IMediator>();
            var handler = _container.Resolve<IAsyncNotificationHandler<TestNotification>>() as TestNotificationHandler;
            var notification = new TestNotification { Message = "TestMessage" };

            yield return mediator.PublishAsync(notification).ToCoroutine();

            Assert.That(handler.Received, Does.Contain("TestMessage"));
        }
#endif
        [Test]
        public void Send_SyncRequest_ThrowsIfHandlerNotRegistered()
        {
            // Build a container without the handler
            var builder = new ContainerBuilder();
            builder.Register<IMediator>(resolver => new Mediator(
                resolver: type => resolver.Resolve(type),
                multiResolver: type =>
                {
                    var enumerableType = typeof(IEnumerable<>).MakeGenericType(type);
                    return (IEnumerable<object>)resolver.Resolve(enumerableType);
                }
            ), Lifetime.Singleton);
            var emptyContainer = builder.Build();
            var mediator = emptyContainer.Resolve<IMediator>();

            Assert.Throws<VContainerException>(() => mediator.Send(new TestSyncRequest { Input = "fail" }));
        }

        // Separate test for pipeline behaviors
        [Test]
        public void SyncPipelineBehavior_IsApplied()
        {
            var builder = new ContainerBuilder();
            builder.Register<TestSyncHandler>(Lifetime.Transient)
                   .As<IRequestHandler<TestSyncRequest, string>>();
            builder.Register<UpperCaseBehavior>(Lifetime.Transient)
                   .As<IPipelineBehavior<TestSyncRequest, string>>();
            builder.Register<IMediator>(resolver => new Mediator(
                resolver: type => resolver.Resolve(type),
                multiResolver: type =>
                {
                    var enumerableType = typeof(IEnumerable<>).MakeGenericType(type);
                    return (IEnumerable<object>)resolver.Resolve(enumerableType);
                }
            ), Lifetime.Singleton);
            var container = builder.Build();
            var mediator = container.Resolve<IMediator>();

            var request = new TestSyncRequest { Input = "hello" };
            string result = mediator.Send(request);

            Assert.That(result, Is.EqualTo("SYNC: HELLO"));
        }

#if UNIMEDIATOR_UNITASK_INTEGRATION
        [UnityTest]
        public IEnumerator AsyncPipelineBehavior_IsAppliedAndLogs()
        {
            var builder = new ContainerBuilder();
            builder.Register<TestAsyncHandler>(Lifetime.Transient)
                   .As<IAsyncRequestHandler<TestAsyncRequest, int>>();
            var loggingBehavior = new LoggingAsyncBehavior();
            builder.RegisterInstance(loggingBehavior)
                   .As<IAsyncPipelineBehavior<TestAsyncRequest, int>>();
            builder.Register<IMediator>(resolver => new Mediator(
                resolver: type => resolver.Resolve(type),
                multiResolver: type =>
                {
                    var enumerableType = typeof(IEnumerable<>).MakeGenericType(type);
                    return (IEnumerable<object>)resolver.Resolve(enumerableType);
                }
            ), Lifetime.Singleton);
            var container = builder.Build();
            var mediator = container.Resolve<IMediator>();

            var request = new TestAsyncRequest { Value = 7 };
            int result = 0;
            yield return mediator.SendAsync(request).ToCoroutine(r => result = r);

            Assert.That(result, Is.EqualTo(14));
            Assert.That(loggingBehavior.Logs, Has.Count.EqualTo(2));
            Assert.That(loggingBehavior.Logs[0], Is.EqualTo("Before: 7"));
            Assert.That(loggingBehavior.Logs[1], Is.EqualTo("After: 14"));
        }
#endif
    }
}
#endif