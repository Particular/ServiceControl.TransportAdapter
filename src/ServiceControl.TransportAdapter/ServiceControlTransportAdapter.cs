using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Faults;
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
        IIntegrationEventPublishingStrategy integrationEventPublishingStrategy;
        Action<TransportExtensions<TFront>> userTransportCustomization;
        EndpointCollection endpoints;
        IRawEndpointInstance inputForwarder;
        IRawEndpointInstance outputForwarder;

        public ServiceControlTransportAdapter(string baseName, 
            IIntegrationEventPublishingStrategy integrationEventPublishingStrategy,
            Action<TransportExtensions<TFront>> userTransportCustomization = null)
        {
            this.baseName = baseName;
            this.integrationEventPublishingStrategy = integrationEventPublishingStrategy;
            this.userTransportCustomization = userTransportCustomization ?? (t => { });
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
                ConfigureControlEndpoint(RawEndpointConfiguration.Create("Particular.ServiceControl", (context, _) => OnControlMessage(context), poisonMessageQueue)),
                ConfigureRetryEndpoint(RawEndpointConfiguration.Create($"{baseName}.Retry", (context, _) => OnRetryMessage(context), poisonMessageQueue)),
                ConfigureIntegrationEndpoint(RawEndpointConfiguration.Create($"{baseName}.Integration", (context, _) => OnIntegrationMessage(context), poisonMessageQueue))
                );
            await endpoints.Start();
        }

        RawEndpointConfiguration ConfigureRetryEndpoint(RawEndpointConfiguration config)
        {
            config.CustomErrorHandlingPolicy(new RetryForwarderErrorPolicy(baseName, "error"));
            config.UseTransport<TBack>();
            config.AutoCreateQueue();
            return config;
        }

        RawEndpointConfiguration ConfigureIntegrationEndpoint(RawEndpointConfiguration config)
        {
            config.CustomErrorHandlingPolicy(new BestEffortPolicy());
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

        RawEndpointConfiguration ConfigureControlEndpoint(RawEndpointConfiguration config)
        {
            config.CustomErrorHandlingPolicy(new BestEffortPolicy());
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
            logger.Info("Forwarding a integration message.");
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var destinations = integrationEventPublishingStrategy.GetDestinations(context.Headers);
            var operations = destinations.Select(d => new TransportOperation(message, d)).ToArray();
            return outputForwarder.SendRaw(new TransportOperations(operations), context.TransportTransaction, context.Context);
        }

        Task OnRetryMessage(MessageContext context)
        {
            var destination = context.Headers[TargetAddressHeader];

            logger.Debug($"Forwarding a retry message to {destination}");

            context.Headers.Remove(TargetAddressHeader);

            return Forward(context, outputForwarder, destination);
        }

        Task OnControlMessage(MessageContext context)
        {
            logger.Debug("Forwarding a control message.");
            return Forward(context, inputForwarder, "Particular.ServiceControl");
        }

        Task OnErrorMessage(MessageContext context)
        {
            context.Headers["ServiceControl.RetryTo"] = $"{baseName}.Retry";
            logger.Debug("Forwarding an error message.");
            return Forward(context, inputForwarder, "error");
        }

        Task OnAuditMessage(MessageContext context)
        {
            logger.Debug("Forwarding an audit message.");
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
            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                return Task.FromResult(ErrorHandleResult.RetryRequired);
            }
        }

        class BestEffortPolicy : IErrorHandlingPolicy
        {
            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                if (handlingContext.Error.ImmediateProcessingFailures < 5)
                {
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                }
                return Task.FromResult(ErrorHandleResult.Handled); //Ignore this message
            }
        }

        class RetryForwarderErrorPolicy : IErrorHandlingPolicy
        {
            string baseName;
            string errorQueue;

            public RetryForwarderErrorPolicy(string baseName, string errorQueue)
            {
                this.baseName = baseName;
                this.errorQueue = errorQueue;
            }
            
            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                if (handlingContext.Error.ImmediateProcessingFailures < 5)
                {
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                }
                var headers = handlingContext.Error.Message.Headers;

                //Will show as if failure occured in the original failure queue.
                var destination = headers["ServiceControl.TargetEndpointAddress"];
                headers.Remove("ServiceControl.TargetEndpointAddress");
                headers["ServiceControl.RetryTo"] = $"{baseName}.Retry";
                headers[FaultsHeaderKeys.FailedQ] = destination;
                return handlingContext.MoveToErrorQueue(errorQueue, false);
            }
        }
    }
}