using System;
using System.Threading.Tasks;
using ConnectionManager;
using NServiceBus;
using NServiceBus.Transport.SQLServer;
using ServiceControl.Contracts;

namespace OtherEndpoint
{
    /// <summary>
    /// This endpoint uses a different database than the SC adapter.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            Console.Title = "OtherEndpoint";
            var config = new EndpointConfiguration("OtherEndpoint");

            var transport = config.UseTransport<SqlServerTransport>();
            transport.ConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=SCAdapter_Other;Integrated Security=True");
            transport.EnableLegacyMultiInstanceMode(ConnectionFactory.GetConnection);

            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("error");
            config.AuditProcessedMessagesTo("audit");
            config.EnableInstallers();
            config.Recoverability().Immediate(i => i.NumberOfRetries(0));
            config.Recoverability().Delayed(d => d.NumberOfRetries(0));
            config.UseSerialization<JsonSerializer>();

            // ReSharper disable once UnusedVariable
            var integrationEndpoint = await Endpoint.Start(BuildIntegrationEventListenerConfig());

            var endpoint = await Endpoint.Start(config);
            
            Console.WriteLine("Press <enter> to send a message the endpoint.");

            while (true)
            {
                Console.ReadLine();
                await endpoint.SendLocal(new OtherMessage());
            }
        }

        static EndpointConfiguration BuildIntegrationEventListenerConfig()
        {
            var config = new EndpointConfiguration("OtherEndpoint.IntegrationListener");
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

    class OtherMessageHandler : IHandleMessages<OtherMessage>
    {
        static readonly Random R = new Random();
        public Task Handle(OtherMessage message, IMessageHandlerContext context)
        {
            if (R.Next(2) == 0)
            {
                throw new Exception("Simulated");
            }
            Console.WriteLine("Processed");
            return Task.FromResult(0);
        }
    }

    class OtherMessage : IMessage
    {
    }
}
