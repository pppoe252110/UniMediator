namespace UniMediator.Runtime
{
    /// <summary>
    /// Defines a synchronous pipeline behavior that wraps around the handling of a request.
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IPipelineBehavior<in TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Pipeline behavior handler. Call next() to invoke the next behavior or the actual handler.
        /// </summary>
        TResponse Handle(TRequest request, RequestHandlerDelegateSync<TResponse> next);
    }

    /// <summary>
    /// Delegate representing the next step in a synchronous pipeline.
    /// </summary>
    public delegate TResponse RequestHandlerDelegateSync<out TResponse>();
}