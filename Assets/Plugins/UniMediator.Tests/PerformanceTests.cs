using NUnit.Framework;
using System.Diagnostics;
using UniMediator.Runtime;
using UniMediator.Tests;

[TestFixture]
public class PerformanceTests : MediatorTestBase
{
    public class Ping : IRequest<string> { public string Message; }
    public class PingHandler : IRequestHandler<Ping, string>
    {
        public string Handle(Ping request) => request.Message;
    }

    [Test]
    public void Send_SyncRequest_ExecutionTime()
    {
        Register<IRequestHandler<Ping, string>>(new PingHandler());
        var request = new Ping { Message = "test" };

        const int iterations = 100_000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            Mediator.Send(request);
        sw.Stop();

        double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / iterations;
        TestContext.WriteLine($"Average per call: {avgMicroseconds:F3} µs");
        Assert.Less(sw.Elapsed.TotalSeconds, 1.0, "Should complete 100k calls in <1 second");
    }
}