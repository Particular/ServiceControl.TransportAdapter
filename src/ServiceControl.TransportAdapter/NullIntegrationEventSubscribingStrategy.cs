namespace ServiceControl.TransportAdapter
{
    using System.Threading.Tasks;
    using NServiceBus.Raw;

    class NullIntegrationEventSubscribingStrategy : IIntegrationEventSubscribingStrategy
    {
        public Task EnsureSubscribed(IRawEndpoint integrationEventSubscriber, string serviceControlInputQueue)
        {
            return Task.FromResult(0);
        }
    }
}