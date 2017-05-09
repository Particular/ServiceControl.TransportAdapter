using System;
using System.Threading.Tasks;
using NServiceBus;
using ServiceControl.Contracts;

namespace YetAnotherEndpoint
{
    class Program
    {
        static void Main(string[] args)
        {
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            Console.Title = "YetAnotherEndpoint";
            var config = new EndpointConfiguration("YetAnotherEndpoint");

            var transport = config.UseTransport<RabbitMQTransport>();
            transport.ConnectionString("host=localhost");

            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("error");
            config.AuditProcessedMessagesTo("audit");
            config.EnableInstallers();
            config.Recoverability().Immediate(i => i.NumberOfRetries(0));
            config.Recoverability().Delayed(d => d.NumberOfRetries(0));
            config.UseSerialization<JsonSerializer>();

            var integrationEventListenerConfig = BuildIntegrationEventListenerConfig();

            var endpoint = await Endpoint.Start(config);
            // ReSharper disable once UnusedVariable
            var integrationEndpoint = await Endpoint.Start(integrationEventListenerConfig);
            
            Console.WriteLine("Press <enter> to send a message the endpoint.");

            while (true)
            {
                Console.ReadLine();
                await endpoint.SendLocal(new YetAnotherMessage());
            }
        }

        static EndpointConfiguration BuildIntegrationEventListenerConfig()
        {
            var config = new EndpointConfiguration("YetAnotherEndpoint.IntegrationListener");
            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("poison");
            config.EnableInstallers();
            config.Conventions().DefiningEventsAs(t => t.Namespace == "ServiceControl.Contracts" ||
                                                                               (typeof(IEvent).IsAssignableFrom(t) && typeof(IEvent) != t));

            config.UseTransport<MsmqTransport>();

            config.Recoverability()
                .CustomPolicy((recoverabilityConfig, context) => RecoverabilityAction.MoveToError("poison"));
            config.UseSerialization<JsonSerializer>();
            return config;
        }
    }

    class IntegrationHandler :
        IHandleMessages<HeartbeatStopped>,
        IHandleMessages<HeartbeatRestored>,
        IHandleMessages<MessageFailed>
    {
        public Task Handle(HeartbeatStopped message, IMessageHandlerContext context)
        {
            Console.WriteLine($"Endpoint {message.EndpointName} stopped sending heartbeats.");
            return Task.FromResult(0);
        }

        public Task Handle(HeartbeatRestored message, IMessageHandlerContext context)
        {
            Console.WriteLine($"Endpoint {message.EndpointName} resumed sending heartbeats.");
            return Task.FromResult(0);

        }

        public Task Handle(MessageFailed message, IMessageHandlerContext context)
        {
            Console.WriteLine($"Messafe {message.FailedMessageId} failed processing at endpoint {message.ProcessingEndpoint.Name}.");
            return Task.FromResult(0);
        }
    }

    class YetAnotherMessageHandler : IHandleMessages<YetAnotherMessage>
    {
        static readonly Random R = new Random();
        public Task Handle(YetAnotherMessage message, IMessageHandlerContext context)
        {
            if (R.Next(2) == 0)
            {
                throw new Exception("Simulated");
            }
            Console.WriteLine("Processed");
            return Task.FromResult(0);
        }
    }

    class YetAnotherMessage : IMessage
    {
    }
}
