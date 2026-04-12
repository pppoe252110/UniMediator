#if UNIMEDIATOR_UNITASK_INTEGRATION
using Cysharp.Threading.Tasks;
using System.Threading;

namespace UniMediator.Runtime
{
    public interface IAsyncPublisher
    {
        UniTask PublishAsync<TNotification>(
            TNotification notification,
            PublishStrategy strategy = PublishStrategy.Sequential,
            CancellationToken cancellationToken = default)
            where TNotification : INotification;
    }

    public enum PublishStrategy
    {
        Sequential,
        Parallel
    }
}
#endif