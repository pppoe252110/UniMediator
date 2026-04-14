#if UNIMEDIATOR_UNITASK_INTEGRATION
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using UniMediator.Runtime;
using UniMediator.Tests;
using UnityEngine.TestTools;

[TestFixture]
public class AsyncPerformanceTests : MediatorTestBase
{
    public class AsyncPing : IAsyncRequest<string> { }
    public class AsyncPingHandler : IAsyncRequestHandler<AsyncPing, string>
    {
        public async UniTask<string> Handle(AsyncPing request, CancellationToken token)
        {
            await UniTask.Yield(); // minimal async work
            return "pong";
        }
    }

    [UnityTest]
    public IEnumerator SendAsync_ExecutionTime()
    {
        // Since UniTask can run on a thread pool, we move the actual benchmark there.
        // The test coroutine waits for the benchmark to complete.
        yield return UniTask.RunOnThreadPool(async () =>
        {
            Register<IAsyncRequestHandler<AsyncPing, string>>(new AsyncPingHandler());
            var request = new AsyncPing();

            const int iterations = 10_000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
                await Mediator.SendAsync<string>(request);
            sw.Stop();

            double avgUs = sw.Elapsed.TotalMilliseconds * 1000 / iterations;
            TestContext.WriteLine($"Async average: {avgUs:F3} µs");
        }).ToCoroutine();
    }
}
#endif