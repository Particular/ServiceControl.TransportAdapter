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

        string adapterName;
        string frontendControlQueue;
        string backendControlQueue;
        string fontendAuditQueue;
        string backendAuditQueue;
        string frontendErrorQueue;
        string backendErrorQueue;
        IIntegrationEventPublishingStrategy integrationEventPublishingStrategy;
        IIntegrationEventSubscribingStrategy integrationEventSubscribingStrategy;
        Action<TransportExtensions<TFront>> frontendTransportCustomization;
        Action<TransportExtensions<TBack>> backendTransportCustomization;
        EndpointCollection endpoints;
        IReceivingRawEndpoint inputForwarder;
        IReceivingRawEndpoint outputForwarder;
        IReceivingRawEndpoint integrationEndpoint;
        string poisonMessageQueueName;

        internal ServiceControlTransportAdapter(string adapterName,
            string frontendControlQueue,
            string backendControlQueue,
            string fontendAuditQueue,
            string backendAuditQueue,
            string frontendErrorQueue,
            string backendErrorQueue,
            string poisonMessageQueueName,
            Action<TransportExtensions<TFront>> frontendTransportCustomization,
            Action<TransportExtensions<TBack>> backendTransportCustomization,
            IIntegrationEventPublishingStrategy integrationEventPublishingStrategy,
            IIntegrationEventSubscribingStrategy integrationEventSubscribingStrategy)
        {
            this.adapterName = adapterName;
            this.frontendControlQueue = frontendControlQueue;
            this.backendControlQueue = backendControlQueue;
            this.fontendAuditQueue = fontendAuditQueue;
            this.backendAuditQueue = backendAuditQueue;
            this.frontendErrorQueue = frontendErrorQueue;
            this.backendErrorQueue = backendErrorQueue;
            this.frontendTransportCustomization = frontendTransportCustomization;
            this.backendTransportCustomization = backendTransportCustomization;
            this.integrationEventPublishingStrategy = integrationEventPublishingStrategy;
            this.integrationEventSubscribingStrategy = integrationEventSubscribingStrategy;
            this.poisonMessageQueueName = poisonMessageQueueName;
        }

        public async Task Start()
        {
            var inputForwarderConfig = ConfigureRetryEndpoint(RawEndpointConfiguration.CreateSendOnly($"{adapterName}.FrontendForwarder"));
            inputForwarder = await RawEndpoint.Start(inputForwarderConfig).ConfigureAwait(false);

            var outputForwarderConfig = ConfigureFrontendTransport(RawEndpointConfiguration.CreateSendOnly($"{adapterName}.BackendForwarder"));
            outputForwarder = await RawEndpoint.Start(outputForwarderConfig).ConfigureAwait(false);

            endpoints = new EndpointCollection(
                ConfigureFrontendTransport(RawEndpointConfiguration.Create(fontendAuditQueue, (context, _) => OnAuditMessage(context), poisonMessageQueueName)),
                ConfigureFrontendTransport(RawEndpointConfiguration.Create(frontendErrorQueue, (context, _) => OnErrorMessage(context), poisonMessageQueueName)),
                ConfigureControlEndpoint(RawEndpointConfiguration.Create(frontendControlQueue, (context, _) => OnControlMessage(context), poisonMessageQueueName)),
                ConfigureRetryEndpoint(RawEndpointConfiguration.Create($"{adapterName}.Retry", (context, _) => OnRetryMessage(context), poisonMessageQueueName))
                );
            await endpoints.Start().ConfigureAwait(false);

            if (integrationEventSubscribingStrategy != null)
            {
                var integrationEndpointConfig = ConfigureIntegrationEndpoint(RawEndpointConfiguration.Create($"{adapterName}.Integration", (context, _) => OnIntegrationMessage(context), poisonMessageQueueName));
                integrationEndpoint = await RawEndpoint.Start(integrationEndpointConfig).ConfigureAwait(false);
                await integrationEventSubscribingStrategy.EnsureSubscribed(integrationEndpoint, backendControlQueue).ConfigureAwait(false);
            }
        }

        RawEndpointConfiguration ConfigureRetryEndpoint(RawEndpointConfiguration config)
        {
            config.CustomErrorHandlingPolicy(new RetryForwarderErrorPolicy(adapterName, backendErrorQueue));
            var transport = config.UseTransport<TBack>();
            backendTransportCustomization(transport);
            config.AutoCreateQueue();
            return config;
        }

        RawEndpointConfiguration ConfigureIntegrationEndpoint(RawEndpointConfiguration config)
        {
            config.CustomErrorHandlingPolicy(new BestEffortPolicy());
            var transport = config.UseTransport<TBack>();
            backendTransportCustomization(transport);
            config.AutoCreateQueue();
            return config;
        }

        RawEndpointConfiguration ConfigureFrontendTransport(RawEndpointConfiguration config)
        {
            config.CustomErrorHandlingPolicy(new RetryForeverPolicy());
            var extensions = config.UseTransport<TFront>();
            frontendTransportCustomization(extensions);
            config.AutoCreateQueue();
            return config;
        }

        RawEndpointConfiguration ConfigureControlEndpoint(RawEndpointConfiguration config)
        {
            config.CustomErrorHandlingPolicy(new BestEffortPolicy());
            var extensions = config.UseTransport<TFront>();
            frontendTransportCustomization(extensions);
            config.AutoCreateQueue();
            return config;
        }


        public async Task Stop()
        {
            await inputForwarder.Stop().ConfigureAwait(false);
            await outputForwarder.Stop().ConfigureAwait(false);
            if (integrationEndpoint != null)
            {
                await integrationEndpoint.Stop().ConfigureAwait(false);
            }
            await endpoints.Stop().ConfigureAwait(false);
        }

        Task OnIntegrationMessage(MessageContext context)
        {
            logger.Info("Forwarding a integration message.");
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var destinations = integrationEventPublishingStrategy.GetDestinations(context.Headers);
            var operations = destinations.Select(d => new TransportOperation(message, d)).ToArray();
            return outputForwarder.Dispatch(new TransportOperations(operations), context.TransportTransaction, context.Context);
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
            return Forward(context, inputForwarder, backendControlQueue);
        }

        Task OnErrorMessage(MessageContext context)
        {
            context.Headers["ServiceControl.RetryTo"] = $"{adapterName}.Retry";
            logger.Debug("Forwarding an error message.");
            return Forward(context, inputForwarder, backendErrorQueue);
        }

        Task OnAuditMessage(MessageContext context)
        {
            logger.Debug("Forwarding an audit message.");
            return Forward(context, inputForwarder, backendAuditQueue);
        }

        static Task Forward(MessageContext context, IDispatchMessages forwarder, string destination)
        {
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(destination));
            return forwarder.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Context);
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

                string destination;
                if (headers.TryGetValue("ServiceControl.TargetEndpointAddress", out destination))
                {
                    headers[FaultsHeaderKeys.FailedQ] = destination;
                    headers.Remove("ServiceControl.TargetEndpointAddress");
                }
                headers["ServiceControl.RetryTo"] = $"{baseName}.Retry";
                return handlingContext.MoveToErrorQueue(errorQueue, false);
            }
        }
    }
}