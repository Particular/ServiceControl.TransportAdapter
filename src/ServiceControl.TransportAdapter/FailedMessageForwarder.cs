namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Faults;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class FailedMessageForwarder<TEndpoint, TServiceControl>
        where TServiceControl : TransportDefinition, new()
        where TEndpoint : TransportDefinition, new()
    {
        public FailedMessageForwarder(string adapterName, string frontendErrorQueue, string backendErrorQueue, int retryMessageImmeidateRetries, string poisonMessageQueueName, Action<TransportExtensions<TEndpoint>> frontendTransportCustomization, Action<TransportExtensions<TServiceControl>> backendTransportCustomization,
            RedirectRetriedMessages retryRedirectCallback, PreserveHeaders preserveHeadersCallback, RestoreHeaders restoreHeadersCallback)
        {
            this.retryRedirectCallback = retryRedirectCallback;
            this.preserveHeadersCallback = preserveHeadersCallback;
            this.restoreHeadersCallback = restoreHeadersCallback;
            backEndConfig = RawEndpointConfiguration.Create($"{adapterName}.Retry", (context, _) => OnRetryMessage(context), poisonMessageQueueName);
            backEndConfig.CustomErrorHandlingPolicy(new RetryForwardingFailurePolicy(backendErrorQueue, retryMessageImmeidateRetries, () => retryToAddress));
            var backEndTransport = backEndConfig.UseTransport<TServiceControl>();
            backendTransportCustomization(backEndTransport);
            backEndConfig.AutoCreateQueue();

            frontEndConfig = RawEndpointConfiguration.Create(frontendErrorQueue, (context, _) => OnErrorMessage(context, backendErrorQueue), poisonMessageQueueName);
            frontEndConfig.CustomErrorHandlingPolicy(new ErrorForwardingFailurePolicy());
            var frontEndTransport = frontEndConfig.UseTransport<TEndpoint>();
            frontendTransportCustomization(frontEndTransport);
            frontEndConfig.AutoCreateQueue();
        }

        Task OnErrorMessage(MessageContext context, string backendErrorQueue)
        {
            context.Headers[TransportAdapterHeaders.RetryTo] = retryToAddress;
            logger.Debug("Forwarding an error message.");

            var newHeaders = new Dictionary<string, string>(context.Headers);

            if (newHeaders.TryGetValue(Headers.ReplyToAddress, out string replyTo))
            {
                newHeaders[Headers.ReplyToAddress] = AddressSanitizer.MakeV5CompatibleAddress(replyTo);
                newHeaders[TransportAdapterHeaders.ReplyToAddress] = replyTo;
            }

            preserveHeadersCallback(newHeaders);

            return Forward(newHeaders, context, backEndDispatcher, backendErrorQueue);
        }

        Task OnRetryMessage(MessageContext context)
        {
            var newHeaders = new Dictionary<string, string>(context.Headers);

            var destination = newHeaders[TransportAdapterHeaders.TargetEndpointAddress];

            logger.Debug($"Forwarding a retry message to {destination}");

            newHeaders.Remove(TransportAdapterHeaders.TargetEndpointAddress);

            if (newHeaders.TryGetValue(TransportAdapterHeaders.ReplyToAddress, out string replyTo))
            {
                newHeaders.Remove(TransportAdapterHeaders.ReplyToAddress);
                newHeaders[Headers.ReplyToAddress] = replyTo;
            }

            restoreHeadersCallback(newHeaders);
            return Forward(newHeaders, context, frontEndDispatcher, retryRedirectCallback(destination, context.Headers));

        }

        static Task Forward(Dictionary<string, string> newHeaders, MessageContext context, IDispatchMessages forwarder, string destination)
        {
            var message = new OutgoingMessage(context.MessageId, newHeaders, context.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(destination));
            return forwarder.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions);
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
            IStoppableRawEndpoint stoppedFrontEnd = null;
            IStoppableRawEndpoint stoppedBackEnd = null;

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
        RedirectRetriedMessages retryRedirectCallback;
        PreserveHeaders preserveHeadersCallback;
        RestoreHeaders restoreHeadersCallback;

        string retryToAddress;
        RawEndpointConfiguration backEndConfig;
        RawEndpointConfiguration frontEndConfig;

        IDispatchMessages backEndDispatcher;
        IReceivingRawEndpoint backEnd;
        IDispatchMessages frontEndDispatcher;
        IReceivingRawEndpoint frontEnd;
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
                if (headers.TryGetValue(TransportAdapterHeaders.TargetEndpointAddress, out var destination))
                {
                    headers[FaultsHeaderKeys.FailedQ] = destination;
                    headers.Remove(TransportAdapterHeaders.TargetEndpointAddress);
                }
                headers[TransportAdapterHeaders.RetryTo] = retryTo();
                return handlingContext.MoveToErrorQueue(errorQueue, false);
            }

            string errorQueue;
            int retries;
            Func<string> retryTo;
        }
    }
}