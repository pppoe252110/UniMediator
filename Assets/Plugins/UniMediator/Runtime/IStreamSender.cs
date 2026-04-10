using Cysharp.Threading.Tasks;
using System.Threading;

namespace UniMediator.Runtime
{
    public interface IStreamSender
    {
        IUniTaskAsyncEnumerable<TResponse> CreateStreamAsync<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default);
    }
}