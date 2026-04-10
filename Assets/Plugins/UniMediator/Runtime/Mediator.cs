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
        private static readonly ConcurrentDictionary<Type, Lazy<Action<object, object>>> _syncVoidCache = new();
        private static readonly ConcurrentDictionary<Type, Lazy<Func<object, object, object, object>>> _syncPipelineBehaviorInvokerCache = new();
        private static readonly ConcurrentDictionary<Type, Lazy<Action<object, object>>> _syncNotificationHandlerInvokerCache = new();

        // --- Boxing‑free synchronous request wrapper cache ---
        private static readonly ConcurrentDictionary<Type, object> _syncRequestWrapperCache = new();

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

        #region Boxing‑free Synchronous Request Handler Wrapper

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
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);

            var handler = _resolver(handlerType)
                ?? throw new InvalidOperationException($"Handler not registered for {requestType.Name}");

            var behaviors = ResolveSyncBehaviors(requestType, responseType).Cast<object>().ToList();

            // Use the boxing‑free wrapper for the final handler invocation
            var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, responseType);
            var wrapper = (IRequestHandlerWrapper<TResponse>)_syncRequestWrapperCache.GetOrAdd(wrapperType,
                _ => Activator.CreateInstance(wrapperType));

            RequestHandlerDelegateSync<TResponse> handlerDelegate = () =>
                wrapper.Handle(request, handler);

            // Apply pipeline behaviors (these still use object casting; see note below)
            foreach (var behaviorObj in behaviors.Reverse<object>())
            {
                var next = handlerDelegate;
                var invoker = GetSyncPipelineBehaviorInvoker(behaviorObj.GetType(), requestType, responseType);
                handlerDelegate = () => (TResponse)invoker(behaviorObj, request, next);
            }

            return handlerDelegate();
        }

        public void Send(IRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var requestType = request.GetType();
            var handlerType = typeof(IRequestHandler<>).MakeGenericType(requestType);

            var handler = _resolver(handlerType)
                ?? throw new InvalidOperationException($"Handler not registered for {requestType.Name}");

            var invoker = _syncVoidCache.GetOrAdd(handlerType,
                new Lazy<Action<object, object>>(() => CreateSyncVoidInvoker(handlerType))).Value;
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

            var invoker = GetSyncNotificationHandlerInvoker(notificationType);
            foreach (var handler in handlers)
                invoker(handler, notification);
        }

        #endregion

        #region Async Method Signatures (implemented in partial files)

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

        #endregion

        #region Pipeline Resolution Helpers

        private IEnumerable<object> ResolveSyncBehaviors(Type requestType, Type responseType)
        {
            var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
            return _multiResolver(behaviorType) ?? Enumerable.Empty<object>();
        }

        private IEnumerable<object> ResolveAsyncBehaviors(Type requestType, Type responseType)
        {
            var behaviorType = typeof(IAsyncPipelineBehavior<,>).MakeGenericType(requestType, responseType);
            return _multiResolver(behaviorType) ?? Enumerable.Empty<object>();
        }

        #endregion

        #region Sync Invoker Builders (Expression Trees)

        private static Action<object, object> CreateSyncVoidInvoker(Type handlerType)
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

        private static Func<object, object, object, object> CreateSyncPipelineBehaviorInvoker(
            Type behaviorType, Type requestType, Type responseType)
        {
            var delegateType = typeof(RequestHandlerDelegateSync<>).MakeGenericType(responseType);

            var behaviorParam = Expression.Parameter(typeof(object), "behavior");
            var requestParam = Expression.Parameter(typeof(object), "request");
            var nextParam = Expression.Parameter(typeof(object), "next");

            var castBehavior = Expression.Convert(behaviorParam, behaviorType);
            var castRequest = Expression.Convert(requestParam, requestType);
            var castNext = Expression.Convert(nextParam, delegateType);

            var method = behaviorType.GetMethod("Handle", new[] { requestType, delegateType });
            var call = Expression.Call(castBehavior, method, castRequest, castNext);
            var castResult = Expression.Convert(call, typeof(object));

            return Expression.Lambda<Func<object, object, object, object>>(
                castResult, behaviorParam, requestParam, nextParam).Compile();
        }

        private Func<object, object, object, object> GetSyncPipelineBehaviorInvoker(
            Type behaviorType, Type requestType, Type responseType)
        {
            var lazy = _syncPipelineBehaviorInvokerCache.GetOrAdd(behaviorType,
                new Lazy<Func<object, object, object, object>>(() =>
                    CreateSyncPipelineBehaviorInvoker(behaviorType, requestType, responseType)));
            return lazy.Value;
        }

        private static Action<object, object> CreateSyncNotificationHandlerInvoker(Type notificationType)
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

        private static Action<object, object> GetSyncNotificationHandlerInvoker(Type notificationType)
        {
            return _syncNotificationHandlerInvokerCache.GetOrAdd(notificationType,
                new Lazy<Action<object, object>>(() => CreateSyncNotificationHandlerInvoker(notificationType))).Value;
        }

        #endregion
    }
}