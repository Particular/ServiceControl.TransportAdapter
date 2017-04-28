namespace ServiceControl.TransportAdapter
{
    using System.Collections.Generic;
    using NServiceBus.Routing;

    public interface IIntegrationEventPublishingStrategy
    {
        IEnumerable<AddressTag> GetDestinations(Dictionary<string, string> headers);
    }
}