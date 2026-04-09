namespace UniMediator.Runtime
{
    public interface IRequest { }
    public interface IRequest<out TResponse> { }

    public interface IRequestHandler<in TRequest>
        where TRequest : IRequest
    {
        void Handle(TRequest request);
    }

    public interface IRequestHandler<in TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        TResponse Handle(TRequest request);
    }
}