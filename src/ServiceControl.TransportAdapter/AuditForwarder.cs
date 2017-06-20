namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class AuditForwarder<TEndpoint, TServiceControl>
        where TEndpoint : TransportDefinition, new()
        where TServiceControl : TransportDefinition, new()
    {
        public AuditForwarder(string adapterName, string fontendAuditQueue, string backendAuditQueue, string poisonMessageQueueName,
            Action<TransportExtensions<TEndpoint>> frontendTransportCustomization, Action<TransportExtensions<TServiceControl>> backendTransportCustomization)
        {
            frontEndConfig = RawEndpointConfiguration.Create(fontendAuditQueue, (context, _) => OnAuditMessage(context, backendAuditQueue), poisonMessageQueueName);
            frontEndConfig.CustomErrorHandlingPolicy(new RetryForeverPolicy());
            var transport = frontEndConfig.UseTransport<TEndpoint>();
            frontendTransportCustomization(transport);
            transport.GetSettings().Set("errorQueue", poisonMessageQueueName);
            frontEndConfig.AutoCreateQueue();

            backEndConfig = RawEndpointConfiguration.CreateSendOnly($"{adapterName}.AuditForwarder");
            var backEndTransport = backEndConfig.UseTransport<TServiceControl>();
            backendTransportCustomization(backEndTransport);
            backEndTransport.GetSettings().Set("errorQueue", poisonMessageQueueName);
        }

        Task OnAuditMessage(MessageContext context, string backendAuditQueue)
        {
            logger.Debug("Forwarding an audit message.");
            return Forward(context, backEnd, backendAuditQueue);
        }

        static Task Forward(MessageContext context, IDispatchMessages forwarder, string destination)
        {
            var message = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(destination));
            return forwarder.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Context);
        }

        public async Task Start()
        {
            backEnd = await RawEndpoint.Start(backEndConfig).ConfigureAwait(false);
            frontEnd = await RawEndpoint.Start(frontEndConfig).ConfigureAwait(false);
        }

        public async Task Stop()
        {
            await frontEnd.Stop().ConfigureAwait(false);
            await backEnd.Stop().ConfigureAwait(false);
        }

        RawEndpointConfiguration backEndConfig;
        RawEndpointConfiguration frontEndConfig;

        IReceivingRawEndpoint backEnd;
        IReceivingRawEndpoint frontEnd;
        static ILog logger = LogManager.GetLogger(typeof(AuditForwarder<,>));

        class RetryForeverPolicy : IErrorHandlingPolicy
        {
            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                return Task.FromResult(ErrorHandleResult.RetryRequired);
            }
        }
    }
}