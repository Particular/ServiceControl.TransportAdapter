namespace ServiceControl.TransportAdapter.AcceptanceTests.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Faults;
    using NServiceBus.Features;
    using NServiceBus.Pipeline;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    public class ServiceControlFake<TTransport>
        where TTransport : TransportDefinition, new()
    {
        string auditQueue;
        string errorQueue;
        string inputQueue;
        Action<TransportExtensions<TTransport>> transportCustomization;
        IReceivingRawEndpoint auditProcessor;
        IReceivingRawEndpoint errorProcessor;
        IEndpointInstance mainEndpoint;

        public event EventHandler<IncomingMessage> MessageAudited = (s, m) => { };
        public event EventHandler<IncomingMessage> MessageFailed = (s, m) => { };
        public event EventHandler<IncomingMessage> ControlMessage = (s, m) => { };

        public ServiceControlFake(string auditQueue, string errorQueue, string inputQueue, Action<TransportExtensions<TTransport>> transportCustomization)
        {
            this.auditQueue = auditQueue;
            this.errorQueue = errorQueue;
            this.inputQueue = inputQueue;
            this.transportCustomization = transportCustomization;
        }

        public async Task Start()
        {
            var auditProcessorConfig = ConfigureIngestionEndpoint(auditQueue, OnAudit);
            var errorProcessorConfig = ConfigureIngestionEndpoint(errorQueue, OnError);
            var inputProcessorConfig = ConfigureMainEndpoint(inputQueue);

            auditProcessor = await RawEndpoint.Start(auditProcessorConfig).ConfigureAwait(false);
            errorProcessor = await RawEndpoint.Start(errorProcessorConfig).ConfigureAwait(false);
            mainEndpoint = await Endpoint.Start(inputProcessorConfig).ConfigureAwait(false);
        }

        public void Retry(IncomingMessage message)
        {
            //Mimics the behavior of ReturnToSenderDequeuer
            var destination = message.Headers[FaultsHeaderKeys.FailedQ];
            message.Headers["ServiceControl.TargetEndpointAddress"] = destination;

            string retryTo;
            if (!message.Headers.TryGetValue("ServiceControl.RetryTo", out retryTo))
            {
                retryTo = destination;
                message.Headers.Remove("ServiceControl.TargetEndpointAddress");
            }
            var operations = new TransportOperation(new OutgoingMessage(message.MessageId, message.Headers, message.Body), new UnicastAddressTag(retryTo));
            errorProcessor.Dispatch(new TransportOperations(operations), new TransportTransaction(), new ContextBag());
        }

        public async Task Stop()
        {
            await mainEndpoint.Stop().ConfigureAwait(false);
            await errorProcessor.Stop().ConfigureAwait(false);
            await auditProcessor.Stop().ConfigureAwait(false);
        }

        EndpointConfiguration ConfigureMainEndpoint(string queueName)
        {
            var config = new EndpointConfiguration(queueName);
            var transport = config.UseTransport<TTransport>();
            transportCustomization(transport);
            config.LimitMessageProcessingConcurrencyTo(1);
            config.Recoverability().Immediate(i => i.NumberOfRetries(0));
            config.Recoverability().Delayed(d => d.NumberOfRetries(0));
            config.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));
            config.Pipeline.Register(new MessageInterceptorBehavior(x => ControlMessage(this, x)), "Intercepts incoming messages.");
            config.UsePersistence<InMemoryPersistence>();
            config.EnableInstallers();
            config.DisableFeature<TimeoutManager>();
            config.Recoverability().DisableLegacyRetriesSatellite();
            config.SendFailedMessagesTo("poison");
            config.TypesToScanHack(new Type[0]);
            return config;
        }

        RawEndpointConfiguration ConfigureIngestionEndpoint(string queueName, Func<MessageContext, IDispatchMessages, Task> onMessage)
        {
            var config = RawEndpointConfiguration.Create(queueName, onMessage, "poison");
            var transport = config.UseTransport<TTransport>();
            transportCustomization(transport);
            config.AutoCreateQueue();
            return config;
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts");
        }

        Task OnError(MessageContext context, IDispatchMessages dispatcher)
        {
            var msg = new IncomingMessage(context.MessageId, context.Headers, context.Body);
            MessageFailed(this, msg);
            return Task.CompletedTask;
        }

        Task OnAudit(MessageContext context, IDispatchMessages dispatcher)
        {
            var msg = new IncomingMessage(context.MessageId, context.Headers, context.Body);
            MessageAudited(this, msg);
            return Task.CompletedTask;
        }

        class MessageInterceptorBehavior : Behavior<ITransportReceiveContext>
        {
            Action<IncomingMessage> callback;

            public MessageInterceptorBehavior(Action<IncomingMessage> callback)
            {
                this.callback = callback;
            }

            public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                callback(context.Message);
                return Task.CompletedTask;
            }
        }
    }
}
