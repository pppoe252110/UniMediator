#if UNIMEDIATOR_UNITASK_INTEGRATION
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniMediator.Runtime
{
    public partial class Mediator
    {
        // Caches for UniTask async handlers & behaviors
        private static class UniTaskHandlerInvokerCache<TResponse>
        {
            public static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, UniTask<TResponse>>> Cache = new();
        }

        private static class UniTaskVoidHandlerInvokerCache
        {
            public static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, UniTask>> Cache = new();
        }

        private static class UniTaskPipelineBehaviorInvokerCache<TResponse>
        {
            public static readonly ConcurrentDictionary<Type, Func<object, object, object, CancellationToken, UniTask<TResponse>>> Cache = new();
        }

        private static class UniTaskNotificationHandlerInvokerCache
        {
            public static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, UniTask>> Cache = new();
        }

        private static class UniTaskStreamHandlerInvokerCache<TResponse>
        {
            public static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, IUniTaskAsyncEnumerable<TResponse>>> Cache = new();
        }

        #region Public Async API

        public UniTask<TResponse> SendAsync<TResponse>(
            IAsyncRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            SendAsyncCore<TResponse>(request, cancellationToken, out var taskObj);
            return (UniTask<TResponse>)taskObj;
        }

        public UniTask SendAsync(
            IAsyncRequest request,
            CancellationToken cancellationToken = default)
        {
            SendAsyncVoidCore(request, cancellationToken, out var taskObj);
            return (UniTask)taskObj;
        }

        public UniTask PublishAsync<TNotification>(
            TNotification notification,
            PublishStrategy strategy = PublishStrategy.Sequential,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            PublishCore(notification, strategy, cancellationToken, out var taskObj);
            return (UniTask)taskObj;
        }

        public IUniTaskAsyncEnumerable<TResponse> CreateStreamAsync<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            CreateStreamCore<TResponse>(request, cancellationToken, out var enumerableObj);
            return (IUniTaskAsyncEnumerable<TResponse>)enumerableObj;
        }

        #endregion

        #region Core Implementations (called via partial methods)

        private partial void SendAsyncCore<TResponse>(
            IAsyncRequest<TResponse> request,
            CancellationToken cancellationToken,
            out object resultTask)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var requestType = request.GetType();
            var responseType = typeof(TResponse);
            var handlerType = typeof(IAsyncRequestHandler<,>).MakeGenericType(requestType, responseType);

            var handler = _resolver(handlerType)
                ?? throw new InvalidOperationException($"Async handler not registered for {requestType.Name}");

            var behaviors = ResolveAsyncBehaviors(requestType, responseType).Cast<object>().ToList();

            RequestHandlerDelegate<TResponse> handlerDelegate = () =>
            {
                var invoker = GetUniTaskResponseHandlerInvoker<TResponse>(handlerType);
                return invoker(handler, request, cancellationToken);
            };

            foreach (var behaviorObj in behaviors.Reverse<object>())
            {
                var next = handlerDelegate;
                var invoker = GetUniTaskPipelineBehaviorInvoker<TResponse>(behaviorObj.GetType(), requestType, responseType);
                handlerDelegate = () => invoker(behaviorObj, request, next, cancellationToken);
            }

            resultTask = handlerDelegate();
        }

        private partial void SendAsyncVoidCore(
            IAsyncRequest request,
            CancellationToken cancellationToken,
            out object resultTask)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var requestType = request.GetType();
            var handlerType = typeof(IAsyncRequestHandler<>).MakeGenericType(requestType);

            var handler = _resolver(handlerType)
                ?? throw new InvalidOperationException($"Async void handler not registered for {requestType.Name}");

            var invoker = GetUniTaskVoidHandlerInvoker(handlerType);
            resultTask = invoker(handler, request, cancellationToken);
        }

        private partial void PublishCore<TNotification>(
            TNotification notification,
            PublishStrategy strategy,
            CancellationToken cancellationToken,
            out object resultTask)
            where TNotification : INotification
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));

            var notificationType = typeof(TNotification);
            var handlerType = typeof(IAsyncNotificationHandler<>).MakeGenericType(notificationType);

            var handlers = _multiResolver(handlerType)?.OfType<object>().ToArray()
                ?? Array.Empty<object>();

            if (handlers.Length == 0)
            {
                resultTask = UniTask.CompletedTask;
                return;
            }

            var invoker = GetUniTaskNotificationHandlerInvoker(notificationType);

            async UniTask PublishAsync()
            {
                if (strategy == PublishStrategy.Sequential)
                {
                    foreach (var handler in handlers)
                        await invoker(handler, notification, cancellationToken);
                }
                else
                {
                    var tasks = new UniTask[handlers.Length];
                    for (int i = 0; i < handlers.Length; i++)
                        tasks[i] = invoker(handlers[i], notification, cancellationToken);
                    await UniTask.WhenAll(tasks);
                }
            }

            resultTask = PublishAsync();
        }

        private partial void CreateStreamCore<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken,
            out object asyncEnumerable)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var requestType = request.GetType();
            var responseType = typeof(TResponse);
            var handlerType = typeof(IStreamHandler<,>).MakeGenericType(requestType, responseType);

            var handler = _resolver(handlerType)
                ?? throw new InvalidOperationException($"Stream handler not registered for {requestType.Name}");

            var invoker = GetUniTaskStreamHandlerInvoker<TResponse>(handlerType);
            asyncEnumerable = invoker(handler, request, cancellationToken);
        }

        #endregion

        #region UniTask Invoker Builders

        private static Func<object, object, CancellationToken, UniTask<TResponse>> GetUniTaskResponseHandlerInvoker<TResponse>(Type handlerType)
        {
            return UniTaskHandlerInvokerCache<TResponse>.Cache.GetOrAdd(handlerType, _ =>
                CreateUniTaskResponseHandlerInvoker<TResponse>(handlerType));
        }

        private static Func<object, object, CancellationToken, UniTask<TResponse>> CreateUniTaskResponseHandlerInvoker<TResponse>(Type handlerType)
        {
            var args = handlerType.GetGenericArguments();
            var requestType = args[0];

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var requestParam = Expression.Parameter(typeof(object), "request");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var castHandler = Expression.Convert(handlerParam, handlerType);
            var castRequest = Expression.Convert(requestParam, requestType);

            var method = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) });
            var call = Expression.Call(castHandler, method, castRequest, ctParam);

            return Expression.Lambda<Func<object, object, CancellationToken, UniTask<TResponse>>>(
                call, handlerParam, requestParam, ctParam).Compile();
        }

        private static Func<object, object, CancellationToken, UniTask> GetUniTaskVoidHandlerInvoker(Type handlerType)
        {
            return UniTaskVoidHandlerInvokerCache.Cache.GetOrAdd(handlerType, _ =>
                CreateUniTaskVoidHandlerInvoker(handlerType));
        }

        private static Func<object, object, CancellationToken, UniTask> CreateUniTaskVoidHandlerInvoker(Type handlerType)
        {
            var args = handlerType.GetGenericArguments();
            var requestType = args[0];

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var requestParam = Expression.Parameter(typeof(object), "request");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var castHandler = Expression.Convert(handlerParam, handlerType);
            var castRequest = Expression.Convert(requestParam, requestType);

            var method = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) });
            var call = Expression.Call(castHandler, method, castRequest, ctParam);

            return Expression.Lambda<Func<object, object, CancellationToken, UniTask>>(
                call, handlerParam, requestParam, ctParam).Compile();
        }

        private static Func<object, object, object, CancellationToken, UniTask<TResponse>> GetUniTaskPipelineBehaviorInvoker<TResponse>(
            Type behaviorType, Type requestType, Type responseType)
        {
            return UniTaskPipelineBehaviorInvokerCache<TResponse>.Cache.GetOrAdd(behaviorType, _ =>
                CreateUniTaskPipelineBehaviorInvoker<TResponse>(behaviorType, requestType, responseType));
        }

        private static Func<object, object, object, CancellationToken, UniTask<TResponse>> CreateUniTaskPipelineBehaviorInvoker<TResponse>(
            Type behaviorType, Type requestType, Type responseType)
        {
            var delegateType = typeof(RequestHandlerDelegate<>).MakeGenericType(responseType);

            var behaviorParam = Expression.Parameter(typeof(object), "behavior");
            var requestParam = Expression.Parameter(typeof(object), "request");
            var nextParam = Expression.Parameter(typeof(object), "next");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var castBehavior = Expression.Convert(behaviorParam, behaviorType);
            var castRequest = Expression.Convert(requestParam, requestType);
            var castNext = Expression.Convert(nextParam, delegateType);

            var method = behaviorType.GetMethod("Handle", new[] { requestType, delegateType, typeof(CancellationToken) });
            var call = Expression.Call(castBehavior, method, castRequest, castNext, ctParam);

            return Expression.Lambda<Func<object, object, object, CancellationToken, UniTask<TResponse>>>(
                call, behaviorParam, requestParam, nextParam, ctParam).Compile();
        }

        private static Func<object, object, CancellationToken, UniTask> GetUniTaskNotificationHandlerInvoker(Type notificationType)
        {
            return UniTaskNotificationHandlerInvokerCache.Cache.GetOrAdd(notificationType, _ =>
                CreateUniTaskNotificationHandlerInvoker(notificationType));
        }

        private static Func<object, object, CancellationToken, UniTask> CreateUniTaskNotificationHandlerInvoker(Type notificationType)
        {
            var handlerType = typeof(IAsyncNotificationHandler<>).MakeGenericType(notificationType);

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var notificationParam = Expression.Parameter(typeof(object), "notification");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var castHandler = Expression.Convert(handlerParam, handlerType);
            var castNotification = Expression.Convert(notificationParam, notificationType);

            var method = handlerType.GetMethod("HandleAsync", new[] { notificationType, typeof(CancellationToken) });
            var call = Expression.Call(castHandler, method, castNotification, ctParam);

            return Expression.Lambda<Func<object, object, CancellationToken, UniTask>>(
                call, handlerParam, notificationParam, ctParam).Compile();
        }

        private static Func<object, object, CancellationToken, IUniTaskAsyncEnumerable<TResponse>> GetUniTaskStreamHandlerInvoker<TResponse>(Type handlerType)
        {
            return UniTaskStreamHandlerInvokerCache<TResponse>.Cache.GetOrAdd(handlerType, _ =>
                CreateUniTaskStreamHandlerInvoker<TResponse>(handlerType));
        }

        private static Func<object, object, CancellationToken, IUniTaskAsyncEnumerable<TResponse>> CreateUniTaskStreamHandlerInvoker<TResponse>(Type handlerType)
        {
            var args = handlerType.GetGenericArguments();
            var requestType = args[0];

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var requestParam = Expression.Parameter(typeof(object), "request");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var castHandler = Expression.Convert(handlerParam, handlerType);
            var castRequest = Expression.Convert(requestParam, requestType);

            var method = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) });
            var call = Expression.Call(castHandler, method, castRequest, ctParam);

            return Expression.Lambda<Func<object, object, CancellationToken, IUniTaskAsyncEnumerable<TResponse>>>(
                call, handlerParam, requestParam, ctParam).Compile();
        }

        #endregion
    }
}
#endif