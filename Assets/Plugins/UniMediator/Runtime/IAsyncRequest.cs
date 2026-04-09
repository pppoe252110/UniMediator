using Cysharp.Threading.Tasks;
using System.Threading;

namespace UniMediator.Runtime
{
    public interface IAsyncRequest { }
    public interface IAsyncRequest<TResponse> { }

    public interface IAsyncRequestHandler<in TRequest>
        where TRequest : IAsyncRequest
    {
        UniTask Handle(TRequest request, CancellationToken cancellationToken);
    }

    public interface IAsyncRequestHandler<in TRequest, TResponse>
        where TRequest : IAsyncRequest<TResponse>
    {
        UniTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    }
}