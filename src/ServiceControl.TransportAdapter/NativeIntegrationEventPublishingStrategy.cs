namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Routing;

    class NativeIntegrationEventPublishingStrategy : IIntegrationEventPublishingStrategy
    {
        public IEnumerable<AddressTag> GetDestinations(Dictionary<string, string> headers)
        {
            var eventType = Type.GetType(headers["NServiceBus.EnclosedMessageTypes"], true);
            yield return new MulticastAddressTag(eventType);
        }
    }
}