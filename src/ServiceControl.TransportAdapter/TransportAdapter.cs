namespace ServiceControl.TransportAdapter
{
    using NServiceBus.Transport;

    public static class TransportAdapter
    {
        /// <summary>
        /// Creates a new transport adapter instance based on the provided configuration.
        /// </summary>
        /// <typeparam name="TEndpoint">Endpoint-side transport.</typeparam>
        /// <typeparam name="TServiceControl">ServiceControl-side transport.</typeparam>
        /// <param name="config">Configuration.</param>
        public static ITransportAdapter Create<TEndpoint, TServiceControl>(TransportAdapterConfig<TEndpoint, TServiceControl> config)
            where TEndpoint : TransportDefinition, new()
            where TServiceControl : TransportDefinition, new()
        {
            var failedMessageForwarder = new FailedMessageForwarder<TEndpoint, TServiceControl>(config.Name, config.EndpointSideErrorQueue, config.ServiceControlSideErrorQueue,
                config.RetryForwardingImmediateRetries, config.PoisonMessageQueue, config.FrontendTransportCustomization, config.BackendTransportCustomization);

            var controlMessageForwarder = new ControlForwarder<TEndpoint, TServiceControl>(config.Name, config.EndpointSideControlQueue, config.ServiceControlSideControlQueue,
                config.PoisonMessageQueue, config.FrontendTransportCustomization, config.BackendTransportCustomization, config.ControlForwardingImmediateRetries,
                config.IntegrationForwardingImmediateRetries, config.IntegrationEventPublishingStrategy, config.IntegrationEventSubscribingStrategy ?? new NullIntegrationEventSubscribingStrategy());

            var auditForwarder = new AuditForwarder<TEndpoint, TServiceControl>(config.Name, config.EndpointSideAuditQueue, config.ServiceControlSideAuditQueue, config.PoisonMessageQueue,
                config.FrontendTransportCustomization, config.BackendTransportCustomization);

            return new ServiceControlTransportAdapter<TEndpoint, TServiceControl>(failedMessageForwarder, controlMessageForwarder, auditForwarder);
        }
    }
}