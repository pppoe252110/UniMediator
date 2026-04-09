using Cysharp.Threading.Tasks;
using System.Threading;

namespace UniMediator.Runtime
{
    public interface ISender
    {
        TResponse Send<TResponse>(IRequest<TResponse> request);
        void Send(IRequest request);

        UniTask<TResponse> SendAsync<TResponse>(
            IAsyncRequest<TResponse> request,
            CancellationToken cancellationToken = default);
        UniTask SendAsync(
            IAsyncRequest request,
            CancellationToken cancellationToken = default);

    }
}