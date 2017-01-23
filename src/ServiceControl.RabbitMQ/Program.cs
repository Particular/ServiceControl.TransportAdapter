using System;
using System.Threading.Tasks;
using NServiceBus;
using ServiceControl.TransportAdapter;

namespace ServiceControl.RabbitMQ
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "ServiceControl.RabbitMQ";
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            var adapter = new ServiceControlTransportAdapter<RabbitMQTransport, MsmqTransport>("ServiceControl.RabbitMQ", InitializeTransport);

            adapter.ConfigureIntegrationEventForwarding(
                new UnicastIntegrationEventPublishingStrategy("YetAnotherEndpoint.IntegrationListener"),
                new UnicastIntegrationEventSubscribingStrategy());

            await adapter.Start();

            Console.WriteLine("Press <enter> to shutdown adapter.");
            Console.ReadLine();

            await adapter.Stop();
        }

        static void InitializeTransport(TransportExtensions<RabbitMQTransport> transport)
        {
            transport.ConnectionString("host=localhost");
        }
    }
}
