using Cysharp.Threading.Tasks;
using System.Threading;

namespace UniMediator.Runtime
{
    /// <summary>
    /// Marker interface for a stream request that yields multiple responses over time.
    /// </summary>
    public interface IStreamRequest<out TResponse> { }

    /// <summary>
    /// Handler for a stream request that returns an async enumerable of responses.
    /// </summary>
    public interface IStreamHandler<in TRequest, out TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        IUniTaskAsyncEnumerable<TResponse> Handle(
            TRequest request,
            CancellationToken cancellationToken = default);

    }
}