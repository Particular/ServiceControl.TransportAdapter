namespace ServiceControl.TransportAdapter
{
    using NServiceBus.Transport;

    public static class TransportAdapter
    {
        /// <summary>
        /// Creates a new transport adapter instance based on the provided configuration.
        /// </summary>
        /// <typeparam name="TFront">Endpoint-side transport.</typeparam>
        /// <typeparam name="TBack">ServiceControl-side transport.</typeparam>
        /// <param name="config">Configuration.</param>
        public static ITransportAdapter Create<TFront, TBack>(TransportAdapterConfig<TFront, TBack> config)
            where TFront : TransportDefinition, new()
            where TBack : TransportDefinition, new()
        {
            var failedMessageForwarder = new FailedMessageForwarder<TFront, TBack>(config.Name, config.EndpointSideErrorQueue, config.ServiceControlSideErrorQueue,
                config.RetryForwardingImmediateRetries, config.PoisonMessageQueue, config.FrontendTransportCustomization, config.BackendTransportCustomization);

            var controlMessageForwarder = new ControlForwarder<TFront, TBack>(config.Name, config.EndpointSideControlQueue, config.ServiceControlSideControlQueue,
                config.PoisonMessageQueue, config.FrontendTransportCustomization, config.BackendTransportCustomization, config.ControlForwardingImmediateRetries,
                config.IntegrationForwardingImmediateRetries, config.IntegrationEventPublishingStrategy, config.IntegrationEventSubscribingStrategy ?? new NullIntegrationEventSubscribingStrategy());

            var auditForwarder = new AuditForwarder<TFront, TBack>(config.Name, config.EndpointSideAuditQueue, config.ServiceControlSideAuditQueue, config.PoisonMessageQueue,
                config.FrontendTransportCustomization, config.BackendTransportCustomization);

            return new ServiceControlTransportAdapter<TFront, TBack>(failedMessageForwarder, controlMessageForwarder, auditForwarder);
        }
    }
}