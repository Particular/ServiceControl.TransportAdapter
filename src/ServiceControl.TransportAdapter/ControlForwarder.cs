namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class ControlForwarder<TEndpoint, TServiceControl>
        where TEndpoint : TransportDefinition, new()
        where TServiceControl : TransportDefinition, new()
    {
        public ControlForwarder(string adapterName, string frontendControlQueue, string backendControlQueue, string backendMonitoringQueue, string poisonMessageQueueName,
            Action<TransportExtensions<TEndpoint>> frontendTransportCustomization, Action<TransportExtensions<TServiceControl>> backendTransportCustomization,
            int controlMessageImmediateRetries, MetricsContext metricsContext)
        {
            var controlMessagesForwarded = metricsContext.Meter("Control messages forwarded", Unit.Custom("Messages"));
            var controlMessageForwardFailures = metricsContext.Meter("Control message forwarding failures", Unit.Custom("Messages"));
            var controlMessagesDropped = metricsContext.Meter("Control messages dropped", Unit.Custom("Messages"));

            frontEndConfig = RawEndpointConfiguration.Create(frontendControlQueue, (context, _) => OnControlMessage(context, backendControlQueue, backendMonitoringQueue, controlMessagesForwarded), poisonMessageQueueName);

            frontEndConfig.CustomErrorHandlingPolicy(new BestEffortPolicy(controlMessageImmediateRetries, controlMessageForwardFailures, controlMessagesDropped));
            var frontEndTransport = frontEndConfig.UseTransport<TEndpoint>();
            frontendTransportCustomization(frontEndTransport);
            frontEndConfig.AutoCreateQueue();

            backEndConfig = RawEndpointConfiguration.CreateSendOnly($"{adapterName}.Control");
            var backEndTransport = backEndConfig.UseTransport<TServiceControl>();
            backendTransportCustomization(backEndTransport);
            backEndConfig.AutoCreateQueue();
        }
        
        Task OnControlMessage(MessageContext context, string backendControlQueue, string backendMonitoringQueue, Meter controlMessagesForwarded)
        {
            var messageType = context.Headers[Headers.EnclosedMessageTypes];
            if (string.Equals(messageType, "NServiceBus.Metrics.MetricReport", StringComparison.OrdinalIgnoreCase))
            {
                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Forwarding a metrics report to {backendMonitoringQueue}");
                }
                return Forward(context, backEnd, backendMonitoringQueue, controlMessagesForwarded);
            }
            if (logger.IsDebugEnabled)
            {
                logger.Debug($"Forwarding a control message {messageType} to {backendControlQueue}");
            }
            return Forward(context, backEnd, backendControlQueue, controlMessagesForwarded);
        }

        public async Task Start()
        {
            backEnd = await RawEndpoint.Start(backEndConfig).ConfigureAwait(false);
            frontEnd = await RawEndpoint.Start(frontEndConfig).ConfigureAwait(false);
        }

        public async Task Stop()
        {
            if (frontEnd != null)
            {
                await frontEnd.Stop().ConfigureAwait(false);
            }
            if (backEnd != null)
            {
                await backEnd.Stop().ConfigureAwait(false);
            }
        }

        static async Task Forward(MessageContext context, IDispatchMessages forwarder, string destination, Meter meter)
        {
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(destination));
            await forwarder.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Context).ConfigureAwait(false);
            meter.Mark();
        }

        RawEndpointConfiguration backEndConfig;
        RawEndpointConfiguration frontEndConfig;

        IReceivingRawEndpoint backEnd;
        IReceivingRawEndpoint frontEnd;
        static ILog logger = LogManager.GetLogger(typeof(ControlForwarder<,>));

        class BestEffortPolicy : IErrorHandlingPolicy
        {
            public BestEffortPolicy(int maxFailures, Meter failures, Meter dropped)
            {
                this.maxFailures = maxFailures;
                this.failures = failures;
                this.dropped = dropped;
            }

            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                if (handlingContext.Error.ImmediateProcessingFailures < maxFailures)
                {
                    failures.Mark();
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                }
                dropped.Mark();
                return Task.FromResult(ErrorHandleResult.Handled); //Ignore this message
            }

            int maxFailures;
            Meter failures;
            Meter dropped;
        }
    }
}