namespace ServiceControl.TransportAdapter
{
    using System.Dynamic;
    using NServiceBus.Transport;

    public static class TransportAdapter
    {
        public static ServiceControlTransportAdapter<TFront, TBack> Create<TFront, TBack>(TransportAdapterConfig<TFront, TBack> config)
            where TFront : TransportDefinition, new()
            where TBack : TransportDefinition, new()
        {
            var failedMessageForwarder = new FailedMessageForwarder<TFront, TBack>(config.Name, config.FronendErrorQueue, config.BackendErrorQueue, 
                config.RetryForwardingImmediateRetries, config.PoisonMessageQueue, config.FrontendTransportCustomization, config.BackendTransportCustomization);

            var controlMessageForwarder = new ControlForwarder<TFront, TBack>(config.Name, config.FrontendServiceControlQueue, config.BackendServiceControlQueue,
                config.PoisonMessageQueue, config.FrontendTransportCustomization, config.BackendTransportCustomization, config.ControlForwardingImmediateRetries, 
                config.IntegrationForwardingImmediateRetries, config.IntegrationEventPublishingStrategy, config.IntegrationEventSubscribingStrategy ?? new NullIntegrationEventSubscribingStrategy());

            var auditForwarder = new AuditForwarder<TFront, TBack>(config.Name, config.FrontendAuditQueue, config.BackendAuditQueue, config.PoisonMessageQueue, 
                config.FrontendTransportCustomization, config.BackendTransportCustomization);

            return new ServiceControlTransportAdapter<TFront, TBack>(failedMessageForwarder, controlMessageForwarder, auditForwarder);
        }
    }
}