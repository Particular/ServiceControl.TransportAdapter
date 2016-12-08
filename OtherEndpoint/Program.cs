using System;
using System.Threading.Tasks;
using ConnectionManager;
using NServiceBus;
using NServiceBus.Transport.SQLServer;
using ServiceControl.Contracts;

namespace OtherEndpoint
{
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

            config.Conventions().DefiningEventsAs(IsEvent);

            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("error");
            config.AuditProcessedMessagesTo("audit");
            config.EnableInstallers();
            config.Recoverability().Immediate(i => i.NumberOfRetries(0));
            config.Recoverability().Delayed(d => d.NumberOfRetries(0));
            config.UseSerialization<JsonSerializer>();

            var endpoint = await Endpoint.Start(config);
            
            Console.WriteLine("Press <enter> to send a message the endpoint.");

            while (true)
            {
                Console.ReadLine();
                await endpoint.SendLocal(new OtherMessage());
            }
        }

        static bool IsEvent(Type t)
        {
            return t.Namespace == "ServiceControl.Contracts" ||
                (typeof(IEvent).IsAssignableFrom(t) && typeof(IEvent) != t);
        }
    }

    class HeartbeatHandler : 
        IHandleMessages<HeartbeatStopped>,
        IHandleMessages<HeartbeatRestored>
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
