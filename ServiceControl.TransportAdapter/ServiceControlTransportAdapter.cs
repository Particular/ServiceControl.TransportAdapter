using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    public class ServiceControlTransportAdapter<TFront, TBack>
        where TFront : TransportDefinition, new()
        where TBack : TransportDefinition, new()
    {
        const string TargetAddressHeader = "ServiceControl.TargetEndpointAddress";

        ILog logger = LogManager.GetLogger(typeof(ServiceControlTransportAdapter<,>));

        string baseName;
        string[] integrationSubscribers;
        Action<TransportExtensions<TFront>> userTransportCustomization;
        EndpointCollection endpoints;
        IRawEndpointInstance inputForwarder;
        IRawEndpointInstance outputForwarder;
        static Action<TransportExtensions<TFront>> emptyCustomization = t => { };

        public ServiceControlTransportAdapter(string baseName, string[] integrationSubscribers,
            Action<TransportExtensions<TFront>> userTransportCustomization = null)
        {
            this.baseName = baseName;
            this.integrationSubscribers = integrationSubscribers;
            this.userTransportCustomization = userTransportCustomization ?? emptyCustomization;
        }

        public async Task Start()
        {
            var inputForwarderConfig = ConfigureBackendTransport(RawEndpointConfiguration.CreateSendOnly($"{baseName}.FrontendForwarder"));
            inputForwarder = await RawEndpoint.Start(inputForwarderConfig).ConfigureAwait(false);

            var outputForwarderConfig = ConfigureFrontendTransport(RawEndpointConfiguration.CreateSendOnly($"{baseName}.BackendForwarder"));
            outputForwarder = await RawEndpoint.Start(outputForwarderConfig).ConfigureAwait(false);

            endpoints = new EndpointCollection(
                ConfigureFrontendTransport(RawEndpointConfiguration.Create("audit", (context, _) => OnAuditMessage(context))),
                ConfigureFrontendTransport(RawEndpointConfiguration.Create("error", (context, _) => OnErrorMessage(context))),
                ConfigureFrontendTransport(RawEndpointConfiguration.Create("Particular.ServiceControl", (context, _) => OnControlMessage(context))),
                ConfigureBackendTransport(RawEndpointConfiguration.Create($"{baseName}.Retry", (context, _) => OnRetryMessage(context))),
                ConfigureBackendTransport(RawEndpointConfiguration.Create($"{baseName}.Integration", (context, _) => OnIntegrationMessage(context)))
                );
            await endpoints.Start();
        }

        RawEndpointConfiguration ConfigureBackendTransport(RawEndpointConfiguration config)
        {
            config.SendFailedMessagesTo("error");
            config.UseTransport<TBack>();
            return config;
        }

        RawEndpointConfiguration ConfigureFrontendTransport(RawEndpointConfiguration config)
        {
            config.SendFailedMessagesTo("error");
            var extensions = config.UseTransport<TFront>();
            userTransportCustomization(extensions);
            return config;
        }

        public async Task Stop()
        {
            await inputForwarder.Stop().ConfigureAwait(false);
            await outputForwarder.Stop().ConfigureAwait(false);
            await endpoints.Stop().ConfigureAwait(false);
        }

        Task OnIntegrationMessage(MessageContext context)
        {
            logger.Info($"Forwarding a integration message.");
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var operations = integrationSubscribers.Select(s => new TransportOperation(message, new UnicastAddressTag(s))).ToArray();
            return outputForwarder.SendRaw(new TransportOperations(operations), context.TransportTransaction, context.Context);
        }

        Task OnRetryMessage(MessageContext context)
        {
            var destination = context.Headers[TargetAddressHeader];

            logger.Info($"Forwarding a retry message to {destination}");

            context.Headers.Remove(TargetAddressHeader);

            return Forward(context, outputForwarder, destination);
        }

        Task OnControlMessage(MessageContext context)
        {
            logger.Info($"Forwarding a control message.");
            return Forward(context, inputForwarder, "Particular.ServiceControl");
        }

        Task OnErrorMessage(MessageContext context)
        {
            logger.Info($"Forwarding an error message.");
            return Forward(context, inputForwarder, "error");
        }

        Task OnAuditMessage(MessageContext context)
        {
            logger.Info($"Forwarding an audit message.");
            return Forward(context, inputForwarder, "audit");
        }

        static Task Forward(MessageContext context, IRawEndpointInstance forwarder, string destination)
        {
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(destination));
            return forwarder.SendRaw(new TransportOperations(operation), context.TransportTransaction, context.Context);
        }
    }
}