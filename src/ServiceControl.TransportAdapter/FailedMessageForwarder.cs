namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Faults;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class FailedMessageForwarder<TFront, TBack>
        where TBack : TransportDefinition, new()
        where TFront : TransportDefinition, new()
    {
        public FailedMessageForwarder(string adapterName, string frontendErrorQueue, string backendErrorQueue, int retryMessageImmeidateRetries, string poisonMessageQueueName,
            Action<TransportExtensions<TFront>> frontendTransportCustomization,
            Action<TransportExtensions<TBack>> backendTransportCustomization)
        {
            backEndConfig = RawEndpointConfiguration.Create($"{adapterName}.Retry", (context, _) => OnRetryMessage(context), poisonMessageQueueName);
            backEndConfig.CustomErrorHandlingPolicy(new RetryForwardingFailurePolicy(backendErrorQueue, retryMessageImmeidateRetries, () => retryToAddress));
            var backEndTransport = backEndConfig.UseTransport<TBack>();
            backendTransportCustomization(backEndTransport);
            backEndConfig.AutoCreateQueue();

            frontEndConfig = RawEndpointConfiguration.Create(frontendErrorQueue, (context, _) => OnErrorMessage(context, backendErrorQueue), poisonMessageQueueName);
            frontEndConfig.CustomErrorHandlingPolicy(new ErrorForwardingFailurePolicy());
            var frontEndTransport = frontEndConfig.UseTransport<TFront>();
            frontendTransportCustomization(frontEndTransport);
            frontEndConfig.AutoCreateQueue();
        }

        Task OnErrorMessage(MessageContext context, string backendErrorQueue)
        {
            context.Headers["ServiceControl.RetryTo"] = retryToAddress;
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
            var stoppedFronEnd = await frontEnd.StopReceiving().ConfigureAwait(false);
            var stoppedBackEnd = await backEnd.StopReceiving().ConfigureAwait(false);

            await stoppedFronEnd.Stop().ConfigureAwait(false);
            await stoppedBackEnd.Stop().ConfigureAwait(false);
        }

        string retryToAddress;
        RawEndpointConfiguration backEndConfig;
        RawEndpointConfiguration frontEndConfig;

        IDispatchMessages backEndDispatcher;
        IReceivingRawEndpoint backEnd;
        IDispatchMessages frontEndDispatcher;
        IReceivingRawEndpoint frontEnd;
        const string TargetAddressHeader = "ServiceControl.TargetEndpointAddress";
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
                headers["ServiceControl.RetryTo"] = retryTo();
                return handlingContext.MoveToErrorQueue(errorQueue, false);
            }

            string errorQueue;
            int retries;
            Func<string> retryTo;
        }
    }
}