#if UNIMEDIATOR_UNITASK_INTEGRATION
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniMediator.Runtime;

namespace UniMediator.Examples
{
    // Notification
    public class PlayerDiedNotification : INotification
    {
        public string PlayerName;
        public Vector3 DeathPosition;
    }

    // Handlers (async)
    public class UIDeathHandler : IAsyncNotificationHandler<PlayerDiedNotification>
    {
        public async UniTask HandleAsync(PlayerDiedNotification notification, CancellationToken token)
        {
            await UniTask.Delay(100, cancellationToken: token);
            Debug.Log($"UI: Show death screen for {notification.PlayerName}");
        }
    }

    public class AudioDeathHandler : IAsyncNotificationHandler<PlayerDiedNotification>
    {
        public async UniTask HandleAsync(PlayerDiedNotification notification, CancellationToken token)
        {
            Debug.Log($"Audio: Play death sound at {notification.DeathPosition}");
            await UniTask.CompletedTask;
        }
    }

    public class AnalyticsDeathHandler : IAsyncNotificationHandler<PlayerDiedNotification>
    {
        public async UniTask HandleAsync(PlayerDiedNotification notification, CancellationToken token)
        {
            await UniTask.Delay(50);
            Debug.Log($"Analytics: Log death event for {notification.PlayerName}");
        }
    }

    // Usage Example
    public class NotificationExample : MonoBehaviour
    {
        private IMediator _mediator;

        private void Awake()
        {
            // Multi‑registration for notifications
            var services = new Dictionary<System.Type, List<object>>();

            void Add<T>(object instance)
            {
                var t = typeof(T);
                if (!services.ContainsKey(t)) services[t] = new List<object>();
                services[t].Add(instance);
            }

            Add<IAsyncNotificationHandler<PlayerDiedNotification>>(new UIDeathHandler());
            Add<IAsyncNotificationHandler<PlayerDiedNotification>>(new AudioDeathHandler());
            Add<IAsyncNotificationHandler<PlayerDiedNotification>>(new AnalyticsDeathHandler());

            _mediator = new Mediator(
                resolver: t => services.TryGetValue(t, out var list) && list.Count > 0 ? list[0] : null,
                multiResolver: t => services.TryGetValue(t, out var list) ? list : System.Linq.Enumerable.Empty<object>()
            );
        }

        private async void Start()
        {
            var notification = new PlayerDiedNotification
            {
                PlayerName = "Mage",
                DeathPosition = new Vector3(10, 2, 5)
            };

            Debug.Log("Publishing sequential...");
            await _mediator.PublishAsync(notification, PublishStrategy.Sequential);

            Debug.Log("Publishing parallel...");
            await _mediator.PublishAsync(notification, PublishStrategy.Parallel);
        }
    }
}
#endif