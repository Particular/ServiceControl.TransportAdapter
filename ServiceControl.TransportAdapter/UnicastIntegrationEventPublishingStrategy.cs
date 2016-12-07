using System.Collections.Generic;
using System.Linq;
using NServiceBus.Routing;

namespace ServiceControl.TransportAdapter
{
    public class UnicastIntegrationEventPublishingStrategy : IIntegrationEventPublishingStrategy
    {
        AddressTag[] destinations;

        public UnicastIntegrationEventPublishingStrategy(params string[] destinations)
        {
            this.destinations = destinations.Select(d => new UnicastAddressTag(d)).ToArray();
        }

        public IEnumerable<AddressTag> GetDestinations(Dictionary<string, string> headers)
        {
            return destinations;
        }
    }
}