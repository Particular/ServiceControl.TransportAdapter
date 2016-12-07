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
        readonly IIntegrationEventPublishingStrategy integrationEventPublishingStrategy;
        Action<TransportExtensions<TFront>> userTransportCustomization;
        EndpointCollection endpoints;
        IRawEndpointInstance inputForwarder;
        IRawEndpointInstance outputForwarder;
        static Action<TransportExtensions<TFront>> emptyCustomization = t => { };

        public ServiceControlTransportAdapter(string baseName, 
            IIntegrationEventPublishingStrategy integrationEventPublishingStrategy,
            Action<TransportExtensions<TFront>> userTransportCustomization = null)
        {
            this.baseName = baseName;
            this.integrationEventPublishingStrategy = integrationEventPublishingStrategy;
            this.userTransportCustomization = userTransportCustomization ?? emptyCustomization;
        }

        public async Task Start()
        {
            var inputForwarderConfig = ConfigureRetryEndpoint(RawEndpointConfiguration.CreateSendOnly($"{baseName}.FrontendForwarder"));
            inputForwarder = await RawEndpoint.Start(inputForwarderConfig).ConfigureAwait(false);

            var outputForwarderConfig = ConfigureFrontendTransport(RawEndpointConfiguration.CreateSendOnly($"{baseName}.BackendForwarder"));
            outputForwarder = await RawEndpoint.Start(outputForwarderConfig).ConfigureAwait(false);

            var poisonMessageQueue = "poison";
            endpoints = new EndpointCollection(
                ConfigureFrontendTransport(RawEndpointConfiguration.Create("audit", (context, _) => OnAuditMessage(context), poisonMessageQueue)),
                ConfigureFrontendTransport(RawEndpointConfiguration.Create("error", (context, _) => OnErrorMessage(context), poisonMessageQueue)),
                ConfigureFrontendTransport(RawEndpointConfiguration.Create("Particular.ServiceControl", (context, _) => OnControlMessage(context), poisonMessageQueue)),
                ConfigureRetryEndpoint(RawEndpointConfiguration.Create($"{baseName}.Retry", (context, _) => OnRetryMessage(context), poisonMessageQueue)),
                ConfigureIntegrationEndpoint(RawEndpointConfiguration.Create($"{baseName}.Integration", (context, _) => OnIntegrationMessage(context), poisonMessageQueue))
                );
            await endpoints.Start();
        }

        RawEndpointConfiguration ConfigureRetryEndpoint(RawEndpointConfiguration config)
        {
            config.DefaultErrorHandlingPolicy("error", 5);
            config.UseTransport<TBack>();
            config.AutoCreateQueue();
            return config;
        }

        RawEndpointConfiguration ConfigureIntegrationEndpoint(RawEndpointConfiguration config)
        {
            config.CustomErrorHandlingPolicy(new IntegrationRetryPolicy());
            config.UseTransport<TBack>();
            config.AutoCreateQueue();
            return config;
        }

        RawEndpointConfiguration ConfigureFrontendTransport(RawEndpointConfiguration config)
        {
            config.CustomErrorHandlingPolicy(new RetryForeverPolicy());
            var extensions = config.UseTransport<TFront>();
            userTransportCustomization(extensions);
            config.AutoCreateQueue();
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
            var destinations = integrationEventPublishingStrategy.GetDestinations(context.Headers);
            var operations = destinations.Select(d => new TransportOperation(message, d)).ToArray();
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
            context.Headers["ServiceControl.RetryTo"] = $"{baseName}.Retry";
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

        class RetryForeverPolicy : IErrorHandlingPolicy
        {
            public Task<ErrorHandleResult> OnError(ErrorContext errorContext, IDispatchMessages dispatcher, Func<ErrorContext, string, Task<ErrorHandleResult>> sendToErrorQueue)
            {
                return Task.FromResult(ErrorHandleResult.RetryRequired);
            }
        }

        class IntegrationRetryPolicy : IErrorHandlingPolicy
        {
            public Task<ErrorHandleResult> OnError(ErrorContext errorContext, IDispatchMessages dispatcher, Func<ErrorContext, string, Task<ErrorHandleResult>> sendToErrorQueue)
            {
                if (errorContext.ImmediateProcessingFailures < 5)
                {
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                }
                return Task.FromResult(ErrorHandleResult.Handled); //Ignore this message
            }
        }
    }
}