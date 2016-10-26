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
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            var adapter = new ServiceControlAdapter<RabbitMQTransport>("ServiceControl.RabbitMQ", InitializeRabbitMQTransport);

            await adapter.Start();

            Console.WriteLine("Press <enter> to shutdown adapter.");
            Console.ReadLine();

            await adapter.Stop();
        }

        static void InitializeRabbitMQTransport(EndpointConfiguration config, TransportExtensions<RabbitMQTransport> transport)
        {
            transport.ConnectionString("host=localhost");
        }
    }
}
