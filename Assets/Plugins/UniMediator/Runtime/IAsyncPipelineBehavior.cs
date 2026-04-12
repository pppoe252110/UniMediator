#if UNIMEDIATOR_UNITASK_INTEGRATION
using Cysharp.Threading.Tasks;
using System.Threading;

namespace UniMediator.Runtime
{
    /// <summary>
    /// Defines an async pipeline behavior that wraps around the handling of a request.
    /// </summary>
    public interface IAsyncPipelineBehavior<in TRequest, TResponse>
        where TRequest : IAsyncRequest<TResponse>
    {
        UniTask<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Delegate representing the next step in the pipeline.
    /// </summary>
    public delegate UniTask<TResponse> RequestHandlerDelegate<TResponse>();
}
#endif