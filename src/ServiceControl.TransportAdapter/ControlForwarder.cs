namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class ControlForwarder<TEndpoint, TServiceControl>
        where TEndpoint : TransportDefinition, new()
        where TServiceControl : TransportDefinition, new()
    {
        public ControlForwarder(string adapterName, string frontendControlQueue, string backendControlQueue, string poisonMessageQueueName,
            Action<TransportExtensions<TEndpoint>> frontendTransportCustomization, Action<TransportExtensions<TServiceControl>> backendTransportCustomization,
            int controlMessageImmediateRetries)
        {
            frontEndConfig = RawEndpointConfiguration.Create(frontendControlQueue, (context, _) => OnControlMessage(context, backendControlQueue), poisonMessageQueueName);

            frontEndConfig.CustomErrorHandlingPolicy(new BestEffortPolicy(controlMessageImmediateRetries));
            var frontEndTransport = frontEndConfig.UseTransport<TEndpoint>();
            frontEndConfig.AutoCreateQueue();

            // customizations override defaults
            frontendTransportCustomization(frontEndTransport);

            backEndConfig = RawEndpointConfiguration.CreateSendOnly($"{adapterName}.Control");
            var backEndTransport = backEndConfig.UseTransport<TServiceControl>();
            backEndConfig.AutoCreateQueue();

            // customizations override defaults
            backendTransportCustomization(backEndTransport);
        }
        
        Task OnControlMessage(MessageContext context, string backendControlQueue)
        {
            logger.Debug("Forwarding a control message.");
            return Forward(context, backEnd, backendControlQueue);
        }

        public async Task Start()
        {
            backEnd = await RawEndpoint.Start(backEndConfig).ConfigureAwait(false);
            frontEnd = await RawEndpoint.Start(frontEndConfig).ConfigureAwait(false);
        }

        public async Task Stop()
        {
            //null-checks for shutting down if start-up failed
            if (frontEnd != null)
            {
                await frontEnd.Stop().ConfigureAwait(false);
            }
            if (backEnd != null)
            {
                await backEnd.Stop().ConfigureAwait(false);
            }
        }

        static Task Forward(MessageContext context, IDispatchMessages forwarder, string destination)
        {
            if (context.Headers.TryGetValue(Headers.ReplyToAddress, out string replyTo))
            {
                context.Headers[Headers.ReplyToAddress] = AddressSanitizer.MakeV5CompatibleAddress(replyTo);
                context.Headers[TransportAdapterHeaders.ReplyToAddress] = replyTo;
            }

            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(destination));

            return forwarder.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Context);
        }

        RawEndpointConfiguration backEndConfig;
        RawEndpointConfiguration frontEndConfig;

        IReceivingRawEndpoint backEnd;
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