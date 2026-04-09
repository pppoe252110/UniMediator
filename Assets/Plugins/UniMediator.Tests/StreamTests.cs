using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniMediator.Runtime;

namespace UniMediator.Tests
{
    [TestFixture]
    public class StreamTests : MediatorTestBase
    {
        public class StreamRequest : IStreamRequest<int> { public int Count { get; set; } = 5; }

        public class StreamHandler : IStreamHandler<StreamRequest, int>
        {
            public IUniTaskAsyncEnumerable<int> Handle(StreamRequest request, CancellationToken cancellationToken)
            {
                return UniTaskAsyncEnumerable.Create<int>(async (writer, ct) =>
                {
                    for (int i = 0; i < request.Count; i++)
                    {
                        // Use the linked token from the writer's context
                        await UniTask.Delay(10, cancellationToken: ct);
                        await writer.YieldAsync(i);
                    }
                });
            }

        }

        [Test]
        public async Task CreateStream_ReturnsAllItems()
        {
            Register<IStreamHandler<StreamRequest, int>>(new StreamHandler());

            var results = new List<int>();
            var stream = Mediator.CreateStream(new StreamRequest { Count = 3 });

            await foreach (var item in stream)
                results.Add(item);

            Assert.AreEqual(new[] { 0, 1, 2 }, results);
        }

        [Test]
        public async Task CreateStream_WithCancellation_StopsMidEnumeration()
        {
            Register<IStreamHandler<StreamRequest, int>>(new StreamHandler());

            var cts = new CancellationTokenSource();
            cts.CancelAfter(25); // Cancel after ~25ms (before all 5 items complete)

            var results = new List<int>();
            var stream = Mediator.CreateStream(new StreamRequest { Count = 5 }, cts.Token);

            try
            {
                await foreach (var item in stream.WithCancellation(cts.Token))
                    results.Add(item);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            Assert.Less(results.Count, 5);
        }

        [Test]
        public void CreateStream_ThrowsIfHandlerNotFound()
        {
            Assert.Throws<InvalidOperationException>(() => Mediator.CreateStream(new StreamRequest()));
        }

        [Test]
        public void CreateStream_ThrowsIfRequestNull()
        {
            Assert.Throws<ArgumentNullException>(() => Mediator.CreateStream<int>(null));
        }
    }
}