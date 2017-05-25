namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Threading.Tasks;
    using Metrics;
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
        public FailedMessageForwarder(string adapterName, string frontendErrorQueue, string backendErrorQueue, int retryMessageImmeidateRetries, string poisonMessageQueueName,
            Action<TransportExtensions<TEndpoint>> frontendTransportCustomization,
            Action<TransportExtensions<TServiceControl>> backendTransportCustomization,
            MetricsContext metricsContext)
        {
            var errorsForwarded = metricsContext.Meter("Errors forwarded", Unit.Custom("Messages"));
            var errorForwardFailures = metricsContext.Meter("Error forwarding failures", Unit.Custom("Messages"));

            var retriesForwarded = metricsContext.Meter("Retry messages forwarded", Unit.Custom("Messages"));
            var retryForwardFailures = metricsContext.Meter("Retry message forwarding failures", Unit.Custom("Messages"));
            var retryReturns = metricsContext.Meter("Retry messages returned to ServiceControl", Unit.Custom("Messages"));

            backEndConfig = RawEndpointConfiguration.Create($"{adapterName}.Retry", (context, _) => OnRetryMessage(context, retriesForwarded), poisonMessageQueueName);
            backEndConfig.CustomErrorHandlingPolicy(new RetryForwardingFailurePolicy(backendErrorQueue, retryMessageImmeidateRetries, () => retryToAddress, retryForwardFailures, retryReturns));
            var backEndTransport = backEndConfig.UseTransport<TServiceControl>();
            backendTransportCustomization(backEndTransport);
            backEndConfig.AutoCreateQueue();

            frontEndConfig = RawEndpointConfiguration.Create(frontendErrorQueue, (context, _) => OnErrorMessage(context, backendErrorQueue, errorsForwarded), poisonMessageQueueName);
            frontEndConfig.CustomErrorHandlingPolicy(new ErrorForwardingFailurePolicy(errorForwardFailures));
            var frontEndTransport = frontEndConfig.UseTransport<TEndpoint>();
            frontendTransportCustomization(frontEndTransport);
            frontEndConfig.AutoCreateQueue();
        }

        Task OnErrorMessage(MessageContext context, string backendErrorQueue, Meter errorsForwarded)
        {
            if (logger.IsDebugEnabled)
            {
                logger.Debug($"Forwarding the failed message {context.MessageId} to {backendErrorQueue}.");
            }
            context.Headers[RetryToHeader] = retryToAddress;
            return Forward(context, backEndDispatcher, backendErrorQueue, errorsForwarded);
        }

        Task OnRetryMessage(MessageContext context, Meter retriesForwarded)
        {
            var destination = context.Headers[TargetAddressHeader];
            if (logger.IsDebugEnabled)
            {
                logger.Debug($"Forwarding the retried message {context.MessageId} to {destination}.");
            }
            context.Headers.Remove(TargetAddressHeader);
            return Forward(context, frontEndDispatcher, destination, retriesForwarded);
        }

        static async Task Forward(MessageContext context, IDispatchMessages forwarder, string destination, Meter meter)
        {
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(destination));
            await forwarder.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Context).ConfigureAwait(false);
            meter.Mark();
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
            IStoppableRawEnedpoint stoppedFronEnd = null;
            IStoppableRawEnedpoint stoppedBackEnd = null;

            if (frontEnd != null)
            {
                stoppedFronEnd = await frontEnd.StopReceiving().ConfigureAwait(false);
            }
            if (backEnd != null)
            {
                stoppedBackEnd = await backEnd.StopReceiving().ConfigureAwait(false);
            }

            if (stoppedFronEnd != null)
            {
                await stoppedFronEnd.Stop().ConfigureAwait(false);
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
        const string RetryToHeader = "ServiceControl.RetryTo";
        static ILog logger = LogManager.GetLogger(typeof(FailedMessageForwarder<,>));

        class ErrorForwardingFailurePolicy : IErrorHandlingPolicy
        {
            public ErrorForwardingFailurePolicy(Meter meter)
            {
                this.meter = meter;
            }

            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                logger.Info($"Adapter is going to retry forwarding the failed message message '{handlingContext.Error.Message.MessageId}' because of an exception:", handlingContext.Error.Exception);
                meter.Mark();
                return Task.FromResult(ErrorHandleResult.RetryRequired);
            }

            Meter meter;
        }

        class RetryForwardingFailurePolicy : IErrorHandlingPolicy
        {
            public RetryForwardingFailurePolicy(string errorQueue, int retries, Func<string> retryTo, Meter forwardFailures, Meter returns)
            {
                this.errorQueue = errorQueue;
                this.retries = retries;
                this.retryTo = retryTo;
                this.forwardFailures = forwardFailures;
                this.returns = returns;
            }

            public async Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                if (handlingContext.Error.ImmediateProcessingFailures < retries)
                {
                    logger.Info($"Adapter is going to retry forwarding the retried message '{handlingContext.Error.Message.MessageId}' because of an exception:", handlingContext.Error.Exception);
                    forwardFailures.Mark();
                    return ErrorHandleResult.RetryRequired;
                }
                var headers = handlingContext.Error.Message.Headers;

                //Will show as if failure occured in the original failure queue.
                string destination;
                if (headers.TryGetValue(TargetAddressHeader, out destination))
                {
                    headers[FaultsHeaderKeys.FailedQ] = destination;
                    headers.Remove(TargetAddressHeader);
                }
                headers[RetryToHeader] = retryTo();
                logger.Error($"Adapter is going to return the retried message '{handlingContext.Error.Message.MessageId}' back to ServiceControl because of an exception:", handlingContext.Error.Exception);
                var result = await handlingContext.MoveToErrorQueue(errorQueue, false).ConfigureAwait(false);
                returns.Mark();
                return result;
            }

            string errorQueue;
            int retries;
            Func<string> retryTo;
            Meter forwardFailures;
            Meter returns;
        }
    }
}