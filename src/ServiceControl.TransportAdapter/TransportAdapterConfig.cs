namespace ServiceControl.TransportAdapter
{
    using System;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Transport;

    /// <summary>
    /// Configures the ServiceControl transport adapter.
    /// </summary>
    /// <typeparam name="TENdpoint">Endpoints' transport.</typeparam>
    /// <typeparam name="TServiceControl">ServiceControl transport.</typeparam>
    public class TransportAdapterConfig<TENdpoint, TServiceControl>
        where TENdpoint : TransportDefinition, new()
        where TServiceControl : TransportDefinition, new()
    {
        /// <summary>
        /// Creates a new configuration object.
        /// </summary>
        /// <param name="name">Name of the adapter. Used as a prefix for adapter's own queues.</param>
        public TransportAdapterConfig(string name)
        {
            metricsContext = new DefaultMetricsContext(name);
            MetricsConfig = new MetricsConfig(metricsContext);
            Name = name;
        }

        internal string Name { get; }

        internal Action<TransportExtensions<TENdpoint>> FrontendTransportCustomization { get; private set; } = e => { };
        internal Action<TransportExtensions<TServiceControl>> BackendTransportCustomization { get; private set; } = e => { };

        /// <summary>
        /// Gets or sets the endpoint-side error queue -- the error queue configured in the endpoints. Defaults to error.
        /// </summary>
        public string EndpointSideErrorQueue { get; set; } = "error";

        /// <summary>
        /// Gets or sets the ServiceControl-side error queue -- the error queue configured in ServiceControl. Defaults to error.
        /// </summary>
        public string ServiceControlSideErrorQueue { get; set; } = "error";

        /// <summary>
        /// Gets or sets the endpoint-side audit queue -- the audit queue configured in the endpoints. Defaults to audit.
        /// </summary>
        public string EndpointSideAuditQueue { get; set; } = "audit";

        /// <summary>
        /// Gets or sets the ServiceControl-side audit queue -- the error audit configured in ServiceControl. Defaults to audit.
        /// </summary>
        public string ServiceControlSideAuditQueue { get; set; } = "audit";

        /// <summary>
        /// Gets or sets the poison message queue. Messages that can't be forwarded are moved to this queue.
        /// </summary>
        public string PoisonMessageQueue { get; set; } = "poison";

        /// <summary>
        /// Gets or sets the endpoint-side control queue -- the control queue configured in the endpoints. Defaults to
        /// Particular.ServiceControl.
        /// </summary>
        public string EndpointSideControlQueue { get; set; } = "Particular.ServiceControl";

        /// <summary>
        /// Gets or sets the ServiceControl-side control queue -- the ServiceControl input queue. Defaults to
        /// Particular.ServiceControl.
        /// </summary>
        public string ServiceControlSideControlQueue { get; set; } = "Particular.ServiceControl";

        /// <summary>
        /// Gets or sets the ServiceControl-side monitoring queue. Defaults to
        /// Particular.ServiceControl.Monitoring.
        /// </summary>
        public string ServiceControlSideMonitoringQueue { get; set; } = "Particular.ServiceControl.Monitoring";

        /// <summary>
        /// Gets or sets the number of immediate retries to be used when forwarding control messages.
        /// </summary>
        public int ControlForwardingImmediateRetries { get; set; } = 5;

        /// <summary>
        /// Gets or sets the number of immediate retries to be used when forwarding ServiceControl integration messages.
        /// </summary>
        public int IntegrationForwardingImmediateRetries { get; set; } = 5;

        /// <summary>
        /// Gets or sets the number of immediate retries to be used when forwarding retry messages.
        /// </summary>
        public int RetryForwardingImmediateRetries { get; set; } = 5;

        /// <summary>
        /// Configures the metrics.
        /// </summary>
        public MetricsConfig MetricsConfig { get; }

        /// <summary>
        /// Configures the adapter to send metrics data to ServiceControl.
        /// </summary>
        /// <param name="hostId">
        /// Optional custom host ID. Defaults to a hash of executable path and machine name. Uniquely
        /// identifies the adapter in ServiceControl.
        /// </param>
        /// <param name="reportInterval">Optional interval between reports. Defaults to 30 seconds.</param>
        public void SendMetricDataToServiceControl(Guid? hostId = null, TimeSpan? reportInterval = null)
        {
            this.reportInterval = reportInterval ?? TimeSpan.FromSeconds(30);
            this.hostId = hostId;
            sendDataToServiceControl = true;
        }

        /// <summary>
        /// Use provied callback to customize the endpoint-side transport.
        /// </summary>
        /// <param name="customization">Customization function.</param>
        public void CustomizeEndpointTransport(Action<TransportExtensions<TENdpoint>> customization)
        {
            if (customization == null)
            {
                throw new ArgumentNullException(nameof(customization));
            }
            FrontendTransportCustomization = customization;
        }

        /// <summary>
        /// Use provided callback to customize the ServiceControl-side transport.
        /// </summary>
        /// <param name="customization">Customization function.</param>
        public void CustomizeServiceControlTransport(Action<TransportExtensions<TServiceControl>> customization)
        {
            if (customization == null)
            {
                throw new ArgumentNullException(nameof(customization));
            }
            BackendTransportCustomization = customization;
        }

        internal DefaultMetricsContext metricsContext;
        internal bool sendDataToServiceControl;
        internal TimeSpan reportInterval;
        internal Guid? hostId;
    }
}