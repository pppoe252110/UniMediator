#if UNIMEDIATOR_UNITASK_INTEGRATION
using Cysharp.Threading.Tasks;
#endif
using System.Threading;

namespace UniMediator.Runtime
{
    public interface ISender
    {
        TResponse Send<TResponse>(IRequest<TResponse> request);
        void Send(IRequest request);
#if UNIMEDIATOR_UNITASK_INTEGRATION
        UniTask<TResponse> SendAsync<TResponse>(
            IAsyncRequest<TResponse> request,
            CancellationToken cancellationToken = default);
        UniTask SendAsync(
            IAsyncRequest request,
            CancellationToken cancellationToken = default);
#endif
    }
}