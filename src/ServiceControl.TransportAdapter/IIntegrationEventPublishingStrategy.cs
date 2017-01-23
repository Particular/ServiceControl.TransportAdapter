using System.Collections.Generic;
using NServiceBus.Routing;

namespace ServiceControl.TransportAdapter
{
    public interface IIntegrationEventPublishingStrategy
    {
        IEnumerable<AddressTag> GetDestinations(Dictionary<string, string> headers);
    }
}