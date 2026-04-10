namespace UniMediator.Runtime
{
    /// <summary>
    /// Synchronous publisher for notifications.
    /// </summary>
    public interface IPublisher
    {
        /// <summary>
        /// Publishes a notification to all registered synchronous handlers.
        /// </summary>
        /// <typeparam name="TNotification">Notification type.</typeparam>
        /// <param name="notification">The notification to publish.</param>
        void Publish<TNotification>(TNotification notification)
            where TNotification : INotification;
    }
}