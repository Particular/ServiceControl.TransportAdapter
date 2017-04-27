namespace ServiceControl.TransportAdapter
{
    using System;
    using NServiceBus;
    using NServiceBus.Transport;

    public class TransportAdapterConfig<TFront, TBack>
        where TFront : TransportDefinition, new()
        where TBack : TransportDefinition, new()
    {
        internal string Name { get; }
        internal IIntegrationEventPublishingStrategy IntegrationEventPublishingStrategy { get; private set; }
        internal IIntegrationEventSubscribingStrategy IntegrationEventSubscribingStrategy { get; private set; }

        internal Action<TransportExtensions<TFront>> FrontendTransportCustomization { get; private set; } = e => { };
        internal Action<TransportExtensions<TBack>> BackendTransportCustomization { get; private set; }= e => { };

        public string FronendErrorQueue { get; set; } = "error";
        public string BackendErrorQueue { get; set; } = "error";
        public string FrontendAuditQueue { get; set; } = "audit";
        public string BackendAuditQueue { get; set; } = "audit";
        public string PoisonMessageQueue { get; set; } = "poison";
        public string FrontendServiceControlQueue { get; set; } = "Particular.ServiceControl";
        public string BackendServiceControlQueue { get; set; } = "Particular.ServiceControl";

        public int ControlForwardingImmediateRetries { get; set; } = 5;
        public int IntegrationForwardingImmediateRetries { get; set; } = 5;
        public int RetryForwardingImmediateRetries { get; set; } = 5;

        public TransportAdapterConfig(string name)
        {
            this.Name = name;
        }

        public void CustomizeFrontendTransport(Action<TransportExtensions<TFront>> customization)
        {
            if (customization == null)
            {
                throw new ArgumentNullException(nameof(customization));
            }
            FrontendTransportCustomization = customization;
        }

        public void CustomizeBackendTransport(Action<TransportExtensions<TBack>> customization)
        {
            if (customization == null)
            {
                throw new ArgumentNullException(nameof(customization));
            }
            BackendTransportCustomization = customization;
        }

        public void ConfigureIntegrationEventForwarding(IIntegrationEventPublishingStrategy integrationEventPublishingStrategy, IIntegrationEventSubscribingStrategy integrationEventSubscribingStrategy)
        {
            if (integrationEventPublishingStrategy == null)
            {
                throw new ArgumentNullException(nameof(integrationEventPublishingStrategy));
            }
            if (integrationEventSubscribingStrategy == null)
            {
                throw new ArgumentNullException(nameof(integrationEventSubscribingStrategy));
            }
            this.IntegrationEventPublishingStrategy = integrationEventPublishingStrategy;
            this.IntegrationEventSubscribingStrategy = integrationEventSubscribingStrategy;
        }
    }
}