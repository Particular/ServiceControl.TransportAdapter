namespace ServiceControl.TransportAdapter
{
    using System.Threading.Tasks;
    using NServiceBus.Raw;

    /// <summary>
    /// Defines the strategy for subscribing to integration events from service control
    /// </summary>
    public interface IIntegrationEventSubscribingStrategy
    {
        /// <summary>
        /// Ensures the adapter is subscribed to the integration event stream.
        /// </summary>
        Task EnsureSubscribed(IRawEndpoint integrationEventSubscriber, string serviceControlInputQueue);
    }
}