namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class ControlForwarder<TFront, TBack>
        where TFront : TransportDefinition, new()
        where TBack : TransportDefinition, new()
    {
        public ControlForwarder(string adapterName, string frontendControlQueue, string backendControlQueue, string poisonMessageQueueName,
            Action<TransportExtensions<TFront>> frontendTransportCustomization, Action<TransportExtensions<TBack>> backendTransportCustomization,
            int controlMessageImmediateRetries, int integrationMessageImmediateRetries,
            IIntegrationEventPublishingStrategy integrationEventPublishingStrategy,
            IIntegrationEventSubscribingStrategy integrationEventSubscribingStrategy)
        {
            this.backendControlQueue = backendControlQueue;
            this.integrationEventPublishingStrategy = integrationEventPublishingStrategy;
            this.integrationEventSubscribingStrategy = integrationEventSubscribingStrategy;
            frontEndConfig = RawEndpointConfiguration.Create(frontendControlQueue, (context, _) => OnControlMessage(context, backendControlQueue), poisonMessageQueueName);

            frontEndConfig.CustomErrorHandlingPolicy(new BestEffortPolicy(controlMessageImmediateRetries));
            var frontEndTransport = frontEndConfig.UseTransport<TFront>();
            frontendTransportCustomization(frontEndTransport);
            frontEndConfig.AutoCreateQueue();

            backEndConfig = RawEndpointConfiguration.Create($"{adapterName}.Integration", (context, _) => OnIntegrationMessage(context), poisonMessageQueueName);
            backEndConfig.CustomErrorHandlingPolicy(new BestEffortPolicy(integrationMessageImmediateRetries));
            var backEndTransport = backEndConfig.UseTransport<TBack>();
            backendTransportCustomization(backEndTransport);
            backEndConfig.AutoCreateQueue();
        }

        Task OnIntegrationMessage(MessageContext context)
        {
            logger.Debug("Forwarding a integration message.");
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var destinations = integrationEventPublishingStrategy.GetDestinations(context.Headers);
            var operations = destinations.Select(d => new TransportOperation(message, d)).ToArray();
            return frontEndDispatcher.Dispatch(new TransportOperations(operations), context.TransportTransaction, context.Context);
        }

        Task OnControlMessage(MessageContext context, string backendControlQueue)
        {
            logger.Debug("Forwarding a control message.");
            return Forward(context, backEndDispatcher, backendControlQueue);
        }

        public async Task Start()
        {
            var initializedFrontEnd = await RawEndpoint.Create(frontEndConfig).ConfigureAwait(false);
            var initializedBackEnd = await RawEndpoint.Create(backEndConfig).ConfigureAwait(false);

            frontEndDispatcher = initializedFrontEnd;
            backEndDispatcher = initializedBackEnd;

            frontEnd = await initializedFrontEnd.Start().ConfigureAwait(false);
            backEnd = await initializedBackEnd.Start().ConfigureAwait(false);

            await integrationEventSubscribingStrategy.EnsureSubscribed(backEnd, backendControlQueue).ConfigureAwait(false);
        }

        public async Task Stop()
        {
            var stoppedFronEnd = await frontEnd.StopReceiving().ConfigureAwait(false);
            var stoppedBackEnd = await backEnd.StopReceiving().ConfigureAwait(false);

            await stoppedFronEnd.Stop().ConfigureAwait(false);
            await stoppedBackEnd.Stop().ConfigureAwait(false);
        }

        static Task Forward(MessageContext context, IDispatchMessages forwarder, string destination)
        {
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(destination));
            return forwarder.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Context);
        }

        string backendControlQueue;
        IIntegrationEventPublishingStrategy integrationEventPublishingStrategy;
        IIntegrationEventSubscribingStrategy integrationEventSubscribingStrategy;

        RawEndpointConfiguration backEndConfig;
        RawEndpointConfiguration frontEndConfig;

        IDispatchMessages backEndDispatcher;
        IReceivingRawEndpoint backEnd;
        IDispatchMessages frontEndDispatcher;
        IReceivingRawEndpoint frontEnd;
        static ILog logger = LogManager.GetLogger(typeof(ControlForwarder<,>));

        class BestEffortPolicy : IErrorHandlingPolicy
        {
            public BestEffortPolicy(int maxFailures)
            {
                this.maxFailures = maxFailures;
            }

            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                if (handlingContext.Error.ImmediateProcessingFailures < maxFailures)
                {
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                }
                return Task.FromResult(ErrorHandleResult.Handled); //Ignore this message
            }

            int maxFailures;
        }
    }
}