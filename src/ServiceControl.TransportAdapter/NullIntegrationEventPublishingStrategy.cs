namespace ServiceControl.TransportAdapter
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Routing;

    class NullIntegrationEventPublishingStrategy : IIntegrationEventPublishingStrategy
    {
        public IEnumerable<AddressTag> GetDestinations(Dictionary<string, string> headers)
        {
            return Enumerable.Empty<AddressTag>();
        }
    }
}