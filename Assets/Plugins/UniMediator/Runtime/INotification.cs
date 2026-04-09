using Cysharp.Threading.Tasks;
using System.Threading;

namespace UniMediator.Runtime
{
    /// <summary>
    /// Marker interface for a notification that can be published to multiple handlers.
    /// </summary>
    public interface INotification { }

    /// <summary>
    /// Defines an asynchronous handler for a notification.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification being handled.</typeparam>
    public interface INotificationHandler<in TNotification>
        where TNotification : INotification
    {
        /// <summary>
        /// Handles the notification asynchronously.
        /// </summary>
        /// <param name="notification">The notification instance.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A UniTask that completes when the handler finishes.</returns>
        UniTask Handle(TNotification notification, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Defines a synchronous handler for a notification.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification being handled.</typeparam>
    public interface ISyncNotificationHandler<in TNotification>
        where TNotification : INotification
    {
        /// <summary>
        /// Handles the notification synchronously.
        /// </summary>
        /// <param name="notification">The notification instance.</param>
        void Handle(TNotification notification);
    }
}