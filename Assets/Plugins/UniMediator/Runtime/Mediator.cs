using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace UniMediator.Runtime
{
    /// <summary>
    /// High‑performance mediator implementation with compiled expression trees.
    /// Supports synchronous and asynchronous requests, notifications, pipeline behaviors, and streams.
    /// </summary>
    public partial class Mediator : IMediator
    {
        private readonly Func<Type, object> _resolver;
        private readonly Func<Type, IEnumerable<object>> _multiResolver;

        // --- Synchronous caches ---
        private static readonly ConcurrentDictionary<Type, Lazy<Action<object, object>>> _voidCache = new();
        private static readonly ConcurrentDictionary<Type, Lazy<Action<object, object>>> _notificationHandlerInvokerCache = new();

        // Cache for compiled behavior invokers (used during pipeline build)
        private static readonly ConcurrentDictionary<(Type BehaviorType, Type RequestType, Type ResponseType), Delegate> _behaviorInvokerCache = new();

        // Instance cache for compiled Send<TResponse> pipeline delegates
        private readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), Delegate> _sendPipelineCache = new();

        public Mediator(Func<Type, object> resolver) : this(resolver, null) { }

        public Mediator(Func<Type, object> resolver, Func<Type, IEnumerable<object>> multiResolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _multiResolver = multiResolver ?? (t =>
            {
                var instance = _resolver(t);
                return instance != null ? new[] { instance } : Enumerable.Empty<object>();
            });
        }

        #region Synchronous Request Handler Wrapper

        private interface IRequestHandlerWrapper<TResponse>
        {
            TResponse Handle(IRequest<TResponse> request, object handler);
        }

        private sealed class RequestHandlerWrapper<TRequest, TResponse> : IRequestHandlerWrapper<TResponse>
            where TRequest : IRequest<TResponse>
        {
            public static readonly Func<IRequestHandler<TRequest, TResponse>, TRequest, TResponse> HandleFunc =
                (handler, req) => handler.Handle(req);

            public TResponse Handle(IRequest<TResponse> request, object handler)
            {
                return HandleFunc(
                    (IRequestHandler<TRequest, TResponse>)handler,
                    (TRequest)request
                );
            }
        }

        #endregion

        #region Synchronous Send

        public TResponse Send<TResponse>(IRequest<TResponse> request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var requestType = request.GetType();
            var responseType = typeof(TResponse);
            var key = (RequestType: requestType, ResponseType: responseType);

            var pipeline = _sendPipelineCache.GetOrAdd(key, _ =>
            {
                return BuildSendPipelineDelegate<TResponse>(requestType, responseType, this);
            });

            var func = (Func<IRequest<TResponse>, TResponse>)pipeline;
            return func(request);
        }

        private static Delegate BuildSendPipelineDelegate<TResponse>(
            Type requestType,
            Type responseType,
            Mediator mediator)
        {
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
            var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);

            // Resolve handler once (must exist)
            var handler = mediator._resolver(handlerType)
                ?? throw new InvalidOperationException($"Handler not registered for {requestType.Name}");

            // Resolve behaviors once
            var behaviors = mediator._multiResolver(behaviorType)?.ToArray() ?? Array.Empty<object>();

            // Create wrapper for boxing‑free handler call
            var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, responseType);
            var wrapper = Activator.CreateInstance(wrapperType);
            var handleMethod = wrapperType.GetMethod("Handle");

            // Compile a delegate for the base handler invocation
            var requestParam = Expression.Parameter(typeof(IRequest<TResponse>), "request");
            var handlerCall = Expression.Call(
                Expression.Constant(wrapper),
                handleMethod,
                requestParam,
                Expression.Constant(handler));
            var baseHandlerFunc = Expression.Lambda<Func<IRequest<TResponse>, TResponse>>(
                handlerCall, requestParam).Compile();

            // Build pipeline by wrapping with behaviors
            Func<IRequest<TResponse>, TResponse> pipelineFunc = baseHandlerFunc;

            for (int i = behaviors.Length - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                pipelineFunc = CreateBehaviorWrapper(requestType, responseType, behavior, pipelineFunc);
            }

            return pipelineFunc;
        }

        private static Func<IRequest<TResponse>, TResponse> CreateBehaviorWrapper<TResponse>(
            Type requestType,
            Type responseType,
            object behavior,
            Func<IRequest<TResponse>, TResponse> next)
        {
            var behaviorType = behavior.GetType();
            var key = (BehaviorType: behaviorType, RequestType: requestType, ResponseType: responseType);

            // Get or create a compiled invoker for this behavior type
            var invoker = (Func<object, IRequest<TResponse>, RequestHandlerDelegateSync<TResponse>, TResponse>)
                _behaviorInvokerCache.GetOrAdd(key, _ =>
                {
                    return CompileBehaviorInvoker<TResponse>(requestType, responseType, behaviorType);
                });

            return request => invoker(behavior, request, () => next(request));
        }

        private static Func<object, IRequest<TResponse>, RequestHandlerDelegateSync<TResponse>, TResponse>
            CompileBehaviorInvoker<TResponse>(Type requestType, Type responseType, Type behaviorType)
        {
            var behaviorParam = Expression.Parameter(typeof(object), "behavior");
            var requestParam = Expression.Parameter(typeof(IRequest<TResponse>), "request");
            var nextParam = Expression.Parameter(typeof(RequestHandlerDelegateSync<TResponse>), "next");

            var castBehavior = Expression.Convert(behaviorParam, behaviorType);
            var castRequest = Expression.Convert(requestParam, requestType);

            var method = behaviorType.GetMethod("Handle", new[] { requestType, typeof(RequestHandlerDelegateSync<TResponse>) });
            var call = Expression.Call(castBehavior, method, castRequest, nextParam);

            var lambda = Expression.Lambda<Func<object, IRequest<TResponse>, RequestHandlerDelegateSync<TResponse>, TResponse>>(
                call, behaviorParam, requestParam, nextParam);

            return lambda.Compile();
        }

        public void Send(IRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var requestType = request.GetType();
            var handlerType = typeof(IRequestHandler<>).MakeGenericType(requestType);

            var handler = _resolver(handlerType)
                ?? throw new InvalidOperationException($"Handler not registered for {requestType.Name}");

            var invoker = _voidCache.GetOrAdd(handlerType,
                new Lazy<Action<object, object>>(() => CreateVoidInvoker(handlerType))).Value;
            invoker(handler, request);
        }

        #endregion

        #region Synchronous Publish

        public void Publish<TNotification>(TNotification notification)
            where TNotification : INotification
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));

            var notificationType = typeof(TNotification);
            var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);

            var handlers = _multiResolver(handlerType)?.OfType<object>().ToArray()
                ?? Array.Empty<object>();

            if (handlers.Length == 0) return;

            var invoker = GetNotificationHandlerInvoker(notificationType);
            foreach (var handler in handlers)
                invoker(handler, notification);
        }

        #endregion

        #region Async Method Signatures
#if UNIMEDIATOR_UNITASK_INTEGRATION
        private partial void SendAsyncCore<TResponse>(
            IAsyncRequest<TResponse> request,
            CancellationToken cancellationToken,
            out object resultTask);

        private partial void SendAsyncVoidCore(
            IAsyncRequest request,
            CancellationToken cancellationToken,
            out object resultTask);

        private partial void PublishCore<TNotification>(
            TNotification notification,
            PublishStrategy strategy,
            CancellationToken cancellationToken,
            out object resultTask)
            where TNotification : INotification;

        private partial void CreateStreamCore<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken,
            out object asyncEnumerable);
#endif
        #endregion

        #region Sync Invoker Builders (Expression Trees)

        private static Action<object, object> CreateVoidInvoker(Type handlerType)
        {
            var args = handlerType.GetGenericArguments();
            var requestType = args[0];

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var requestParam = Expression.Parameter(typeof(object), "request");

            var castHandler = Expression.Convert(handlerParam, handlerType);
            var castRequest = Expression.Convert(requestParam, requestType);

            var method = handlerType.GetMethod("Handle", new[] { requestType });
            var call = Expression.Call(castHandler, method, castRequest);

            return Expression.Lambda<Action<object, object>>(
                call, handlerParam, requestParam).Compile();
        }

        private static Action<object, object> CreateNotificationHandlerInvoker(Type notificationType)
        {
            var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var notificationParam = Expression.Parameter(typeof(object), "notification");

            var castHandler = Expression.Convert(handlerParam, handlerType);
            var castNotification = Expression.Convert(notificationParam, notificationType);

            var method = handlerType.GetMethod("Handle", new[] { notificationType });
            var call = Expression.Call(castHandler, method, castNotification);

            return Expression.Lambda<Action<object, object>>(call, handlerParam, notificationParam).Compile();
        }

        private static Action<object, object> GetNotificationHandlerInvoker(Type notificationType)
        {
            return _notificationHandlerInvokerCache.GetOrAdd(notificationType,
                new Lazy<Action<object, object>>(() => CreateNotificationHandlerInvoker(notificationType))).Value;
        }

        #endregion
    }
}