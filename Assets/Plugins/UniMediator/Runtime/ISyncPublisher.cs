namespace UniMediator.Runtime
{
    /// <summary>
    /// Synchronous publisher for notifications.
    /// </summary>
    public interface ISyncPublisher
    {
        /// <summary>
        /// Publishes a notification to all registered synchronous handlers.
        /// </summary>
        /// <typeparam name="TNotification">Notification type.</typeparam>
        /// <param name="notification">The notification to publish.</param>
        void PublishSync<TNotification>(TNotification notification)
            where TNotification : INotification;
    }
}