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

    class AuditForwarder<TEndpoint, TServiceControl>
        where TEndpoint : TransportDefinition, new()
        where TServiceControl : TransportDefinition, new()
    {
        public AuditForwarder(string adapterName, string fontendAuditQueue, string backendAuditQueue, string poisonMessageQueueName,
            Action<TransportExtensions<TEndpoint>> frontendTransportCustomization, Action<TransportExtensions<TServiceControl>> backendTransportCustomization,
            MetricsContext metricsContext)
        {
            var auditsForwarded = metricsContext.Meter("Audits forwarded", Unit.Custom("Messages"));
            var auditForwardFailures = metricsContext.Meter("Audit forwarding failures", Unit.Custom("Messages"));

            frontEndConfig = RawEndpointConfiguration.Create(fontendAuditQueue, (context, _) => OnAuditMessage(context, backendAuditQueue, auditsForwarded), poisonMessageQueueName);
            frontEndConfig.CustomErrorHandlingPolicy(new RetryForeverPolicy(auditForwardFailures));
            var extensions = frontEndConfig.UseTransport<TEndpoint>();
            frontendTransportCustomization(extensions);
            frontEndConfig.AutoCreateQueue();

            backEndConfig = RawEndpointConfiguration.CreateSendOnly($"{adapterName}.AuditForwarder");
            var backEndTransport = backEndConfig.UseTransport<TServiceControl>();
            backendTransportCustomization(backEndTransport);
        }

        Task OnAuditMessage(MessageContext context, string backendAuditQueue, Meter meter)
        {
            if (logger.IsDebugEnabled)
            {
                logger.Debug($"Forwarding the audit message {context.MessageId} to {backendAuditQueue}.");
            }
            return Forward(context, backEnd, backendAuditQueue, meter);
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

        RawEndpointConfiguration backEndConfig;
        RawEndpointConfiguration frontEndConfig;

        IReceivingRawEndpoint backEnd;
        IReceivingRawEndpoint frontEnd;
        static ILog logger = LogManager.GetLogger(typeof(AuditForwarder<,>));

        class RetryForeverPolicy : IErrorHandlingPolicy
        {
            public RetryForeverPolicy(Meter meter)
            {
                this.meter = meter;
            }

            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                logger.Info($"Adapter is going to retry forwarding the audit message '{handlingContext.Error.Message.MessageId}' because of an exception:", handlingContext.Error.Exception);
                meter.Mark();
                return Task.FromResult(ErrorHandleResult.RetryRequired);
            }

            Meter meter;
        }
    }
}