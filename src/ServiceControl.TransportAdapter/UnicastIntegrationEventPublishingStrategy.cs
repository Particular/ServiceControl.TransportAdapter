namespace ServiceControl.TransportAdapter
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Routing;

    public class UnicastIntegrationEventPublishingStrategy : IIntegrationEventPublishingStrategy
    {
        public UnicastIntegrationEventPublishingStrategy(params string[] destinations)
        {
            this.destinations = destinations.Select(d => new UnicastAddressTag(d)).ToArray();
        }

        public IEnumerable<AddressTag> GetDestinations(Dictionary<string, string> headers)
        {
            return destinations;
        }

        AddressTag[] destinations;
    }
}