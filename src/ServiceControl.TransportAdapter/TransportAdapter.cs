namespace ServiceControl.TransportAdapter
{
    using NServiceBus.Transport;

    public static class TransportAdapter
    {
        public static ServiceControlTransportAdapter<TFront, TBack> Create<TFront, TBack>(TransportAdapterConfig<TFront, TBack> config)
            where TFront : TransportDefinition, new()
            where TBack : TransportDefinition, new()
        {
            return new ServiceControlTransportAdapter<TFront, TBack>(
                config.Name,
                config.FrontendServiceControlQueue,
                config.BackendServiceControlQueue,
                config.FrontendAuditQueue,
                config.BackendAuditQueue,
                config.FronendErrorQueue,
                config.BackendErrorQueue,
                config.PoisonMessageQueue,
                config.FrontendTransportCustomization,
                config.BackendTransportCustomization,
                config.IntegrationEventPublishingStrategy,
                config.IntegrationEventSubscribingStrategy);
        }
    }
}