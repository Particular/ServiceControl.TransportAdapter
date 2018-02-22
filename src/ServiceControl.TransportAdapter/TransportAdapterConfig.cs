namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Transport;

    /// <summary>
    /// Configures the ServiceControl transport adapter.
    /// </summary>
    /// <typeparam name="TEndpoint">Endpoints' transport.</typeparam>
    /// <typeparam name="TServiceControl">ServiceControl transport.</typeparam>
    public class TransportAdapterConfig<TEndpoint, TServiceControl>
        where TEndpoint : TransportDefinition, new()
        where TServiceControl : TransportDefinition, new()
    {
        internal RedirectRetriedMessages RedirectCallback = (failedQ, headers) => failedQ;
        internal PreserveHeaders PreserveHeadersCallback = x => { };
        internal RestoreHeaders RestoreHeadersCallback = x => { };

        /// <summary>
        /// Creates a new configuration object.
        /// </summary>
        /// <param name="name">Name of the adapter. Used as a prefix for adapter's own queues.</param>
        public TransportAdapterConfig(string name)
        {
            Name = name;
        }

        internal string Name { get; }

        internal Action<TransportExtensions<TEndpoint>> FrontendTransportCustomization { get; private set; } = e => { };
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
        /// Use provied callback to customize the endpoint-side transport.
        /// </summary>
        /// <param name="customization">Customization function.</param>
        public void CustomizeEndpointTransport(Action<TransportExtensions<TEndpoint>> customization)
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

        /// <summary>
        /// Instructs the adapter to redirect retried messages based on the provided callback.
        /// </summary>
        public void RedirectRetriedMessages(RedirectRetriedMessages redirectCallback)
        {
            RedirectCallback = redirectCallback;
        }

        /// <summary>
        /// Instructs the adapter to preserve certain faied message headers and restore them back when retrying the message.
        /// </summary>
        public void PreserveHeaders(PreserveHeaders preserveCallback, RestoreHeaders restoreCallback)
        {
            PreserveHeadersCallback = preserveCallback;
            RestoreHeadersCallback = restoreCallback;
        }
    }

    /// <summary>
    /// Delagate for redirecting retried messages.
    /// </summary>
    /// <param name="failedQ">Address from the original FailedQ header.</param>
    /// <param name="headers">Headers of the message.</param>
    /// <returns>New destination address</returns>
    public delegate string RedirectRetriedMessages(string failedQ, Dictionary<string, string> headers);

    /// <summary>
    /// Callback for copying message headers so that value are not overwritten by ServiceControl.
    /// </summary>
    /// <param name="headers">Message headers.</param>
    public delegate void PreserveHeaders(Dictionary<string, string> headers);

    /// <summary>
    /// Callback for copying preserved message headers back.
    /// </summary>
    /// <param name="headers">Message headers.</param>
    public delegate void RestoreHeaders(Dictionary<string, string> headers);
}