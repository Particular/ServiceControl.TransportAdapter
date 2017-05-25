namespace ServiceControl.TransportAdapter
{
    using System;
    using NServiceBus.Support;
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
            NServiceBusMetricReport<TServiceControl> report = null;
            if (config.sendDataToServiceControl)
            {
                var fullPathToStartingExe = PathUtilities.SanitizedPath(Environment.CommandLine);
                var hostId = config.hostId ?? DeterministicGuid.Create(fullPathToStartingExe, RuntimeEnvironment.MachineName);

                config.MetricsConfig.WithReporting(r =>
                {
                    report = new NServiceBusMetricReport<TServiceControl>(config.Name, hostId, config.ServiceControlSideMonitoringQueue, config.BackendTransportCustomization);
                    r.WithReport(report, config.reportInterval);
                });
            }

            var failedMessageForwarder = new FailedMessageForwarder<TEndpoint, TServiceControl>(config.Name, config.EndpointSideErrorQueue, config.ServiceControlSideErrorQueue,
                config.RetryForwardingImmediateRetries, config.PoisonMessageQueue, config.FrontendTransportCustomization, config.BackendTransportCustomization, config.metricsContext);

            var controlMessageForwarder = new ControlForwarder<TEndpoint, TServiceControl>(config.Name, config.EndpointSideControlQueue, config.ServiceControlSideControlQueue, config.ServiceControlSideMonitoringQueue,
                config.PoisonMessageQueue, config.FrontendTransportCustomization, config.BackendTransportCustomization, config.ControlForwardingImmediateRetries, config.metricsContext);

            var auditForwarder = new AuditForwarder<TEndpoint, TServiceControl>(config.Name, config.EndpointSideAuditQueue, config.ServiceControlSideAuditQueue, config.PoisonMessageQueue,
                config.FrontendTransportCustomization, config.BackendTransportCustomization, config.metricsContext);

            return new ServiceControlTransportAdapter<TEndpoint, TServiceControl>(failedMessageForwarder, controlMessageForwarder, auditForwarder, report);
        }
    }
}