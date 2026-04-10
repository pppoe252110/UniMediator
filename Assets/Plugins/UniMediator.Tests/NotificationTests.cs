using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniMediator.Runtime;

namespace UniMediator.Tests
{
    [TestFixture]
    public class NotificationTests : MediatorTestBase
    {
        public class SampleNotification : INotification { public string Message { get; set; } }
        public class SampleNotificationHandler : IAsyncNotificationHandler<SampleNotification>
        {
            public List<string> ReceivedMessages { get; } = new List<string>();
            public async UniTask HandleAsync(SampleNotification notification, CancellationToken cancellationToken)
            {
                await UniTask.Delay(5, cancellationToken: cancellationToken);
                ReceivedMessages.Add(notification.Message);
            }
        }

        [Test]
        public async Task Publish_Sequential_AllHandlersInvoked()
        {
            var handler1 = new SampleNotificationHandler();
            var handler2 = new SampleNotificationHandler();
            Register<IAsyncNotificationHandler<SampleNotification>>(handler1);
            Register<IAsyncNotificationHandler<SampleNotification>>(handler2);

            await Mediator.PublishAsync(new SampleNotification { Message = "Test" }, PublishStrategy.Sequential);

            Assert.Contains("Test", handler1.ReceivedMessages);
            Assert.Contains("Test", handler2.ReceivedMessages);
        }

        [Test]
        public async Task Publish_Parallel_AllHandlersInvoked()
        {
            var handler1 = new SampleNotificationHandler();
            var handler2 = new SampleNotificationHandler();
            Register<IAsyncNotificationHandler<SampleNotification>>(handler1);
            Register<IAsyncNotificationHandler<SampleNotification>>(handler2);

            await Mediator.PublishAsync(new SampleNotification { Message = "Parallel" }, PublishStrategy.Parallel);

            Assert.Contains("Parallel", handler1.ReceivedMessages);
            Assert.Contains("Parallel", handler2.ReceivedMessages);
        }

        [Test]
        public async Task Publish_NoHandlers_DoesNotThrow()
        {
            await Mediator.PublishAsync(new SampleNotification());
        }

        [Test]
        public void Publish_ThrowsIfNotificationNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () => await Mediator.PublishAsync<SampleNotification>(null));
        }
    }
}
