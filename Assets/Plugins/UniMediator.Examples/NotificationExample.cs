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
    public class UIDeathHandler : INotificationHandler<PlayerDiedNotification>
    {
        public async UniTask Handle(PlayerDiedNotification notification, CancellationToken token)
        {
            await UniTask.Delay(100, cancellationToken: token);
            Debug.Log($"UI: Show death screen for {notification.PlayerName}");
        }
    }

    public class AudioDeathHandler : INotificationHandler<PlayerDiedNotification>
    {
        public async UniTask Handle(PlayerDiedNotification notification, CancellationToken token)
        {
            Debug.Log($"Audio: Play death sound at {notification.DeathPosition}");
            await UniTask.CompletedTask;
        }
    }

    public class AnalyticsDeathHandler : INotificationHandler<PlayerDiedNotification>
    {
        public async UniTask Handle(PlayerDiedNotification notification, CancellationToken token)
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

            Add<INotificationHandler<PlayerDiedNotification>>(new UIDeathHandler());
            Add<INotificationHandler<PlayerDiedNotification>>(new AudioDeathHandler());
            Add<INotificationHandler<PlayerDiedNotification>>(new AnalyticsDeathHandler());

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
            await _mediator.Publish(notification, PublishStrategy.Sequential);

            Debug.Log("Publishing parallel...");
            await _mediator.Publish(notification, PublishStrategy.Parallel);
        }
    }
}