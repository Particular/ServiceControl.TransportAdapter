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
            var config = new EndpointConfiguration("YetAnotherEndpoint");

            var transport = config.UseTransport<RabbitMQTransport>();
            transport.ConnectionString("host=localhost");

            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("ServiceControl.RabbitMQ.error");
            config.AuditProcessedMessagesTo("ServiceControl.RabbitMQ.audit");
            config.EnableInstallers();
            config.Conventions().DefiningEventsAs(IsEvent);
            config.Recoverability().Immediate(i => i.NumberOfRetries(0));
            config.Recoverability().Delayed(d => d.NumberOfRetries(0));

            var endpoint = await Endpoint.Start(config);
            
            Console.WriteLine("Press <enter> to send a message the endpoint.");

            while (true)
            {
                Console.ReadLine();
                await endpoint.SendLocal(new YetAnotherMessage());
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
