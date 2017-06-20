namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Faults;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class FailedMessageForwarder<TEndpoint, TServiceControl>
        where TServiceControl : TransportDefinition, new()
        where TEndpoint : TransportDefinition, new()
    {
        public FailedMessageForwarder(string adapterName, string frontendErrorQueue, string backendErrorQueue, int retryMessageImmeidateRetries, string poisonMessageQueueName,
            Action<TransportExtensions<TEndpoint>> frontendTransportCustomization,
            Action<TransportExtensions<TServiceControl>> backendTransportCustomization)
        {
            backEndConfig = RawEndpointConfiguration.Create($"{adapterName}.Retry", (context, _) => OnRetryMessage(context), poisonMessageQueueName);
            backEndConfig.CustomErrorHandlingPolicy(new RetryForwardingFailurePolicy(backendErrorQueue, retryMessageImmeidateRetries, () => retryToAddress));
            var backEndTransport = backEndConfig.UseTransport<TServiceControl>();
            backendTransportCustomization(backEndTransport);
            backEndTransport.GetSettings().Set("errorQueue", poisonMessageQueueName);
            backEndConfig.AutoCreateQueue();

            frontEndConfig = RawEndpointConfiguration.Create(frontendErrorQueue, (context, _) => OnErrorMessage(context, backendErrorQueue), poisonMessageQueueName);
            frontEndConfig.CustomErrorHandlingPolicy(new ErrorForwardingFailurePolicy());
            var frontEndTransport = frontEndConfig.UseTransport<TEndpoint>();
            frontendTransportCustomization(frontEndTransport);
            frontEndTransport.GetSettings().Set("errorQueue", poisonMessageQueueName);
            frontEndConfig.AutoCreateQueue();
        }

        Task OnErrorMessage(MessageContext context, string backendErrorQueue)
        {
            context.Headers[RetrytoHeader] = retryToAddress;
            logger.Debug("Forwarding an error message.");
            return Forward(context, backEndDispatcher, backendErrorQueue);
        }

        Task OnRetryMessage(MessageContext context)
        {
            var destination = context.Headers[TargetAddressHeader];

            logger.Debug($"Forwarding a retry message to {destination}");

            context.Headers.Remove(TargetAddressHeader);

            return Forward(context, frontEndDispatcher, destination);
        }

        static Task Forward(MessageContext context, IDispatchMessages forwarder, string destination)
        {
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(destination));
            return forwarder.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Context);
        }

        public async Task Start()
        {
            var initializedFrontEnd = await RawEndpoint.Create(frontEndConfig).ConfigureAwait(false);
            var initializedBackEnd = await RawEndpoint.Create(backEndConfig).ConfigureAwait(false);

            frontEndDispatcher = initializedFrontEnd;
            backEndDispatcher = initializedBackEnd;

            retryToAddress = initializedBackEnd.TransportAddress;

            frontEnd = await initializedFrontEnd.Start().ConfigureAwait(false);
            backEnd = await initializedBackEnd.Start().ConfigureAwait(false);
        }

        public async Task Stop()
        {
            //null-checks for shutting down if start-up failed
            IStoppableRawEnedpoint stoppedFrontEnd = null;
            IStoppableRawEnedpoint stoppedBackEnd = null;

            if (frontEnd != null)
            {
                stoppedFrontEnd = await frontEnd.StopReceiving().ConfigureAwait(false);
            }
            if (backEnd != null)
            {
                stoppedBackEnd = await backEnd.StopReceiving().ConfigureAwait(false);
            }

            if (stoppedFrontEnd != null)
            {
                await stoppedFrontEnd.Stop().ConfigureAwait(false);
            }
            if (stoppedBackEnd != null)
            {
                await stoppedBackEnd.Stop().ConfigureAwait(false);
            }
        }

        string retryToAddress;
        RawEndpointConfiguration backEndConfig;
        RawEndpointConfiguration frontEndConfig;

        IDispatchMessages backEndDispatcher;
        IReceivingRawEndpoint backEnd;
        IDispatchMessages frontEndDispatcher;
        IReceivingRawEndpoint frontEnd;
        const string TargetAddressHeader = "ServiceControl.TargetEndpointAddress";
        const string RetrytoHeader = "ServiceControl.RetryTo";
        static ILog logger = LogManager.GetLogger(typeof(FailedMessageForwarder<,>));

        class ErrorForwardingFailurePolicy : IErrorHandlingPolicy
        {
            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                return Task.FromResult(ErrorHandleResult.RetryRequired);
            }
        }

        class RetryForwardingFailurePolicy : IErrorHandlingPolicy
        {
            public RetryForwardingFailurePolicy(string errorQueue, int retries, Func<string> retryTo)
            {
                this.errorQueue = errorQueue;
                this.retries = retries;
                this.retryTo = retryTo;
            }

            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                if (handlingContext.Error.ImmediateProcessingFailures < retries)
                {
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                }
                var headers = handlingContext.Error.Message.Headers;

                //Will show as if failure occured in the original failure queue.
                string destination;
                if (headers.TryGetValue(TargetAddressHeader, out destination))
                {
                    headers[FaultsHeaderKeys.FailedQ] = destination;
                    headers.Remove(TargetAddressHeader);
                }
                headers[RetrytoHeader] = retryTo();
                return handlingContext.MoveToErrorQueue(errorQueue, false);
            }

            string errorQueue;
            int retries;
            Func<string> retryTo;
        }
    }
}