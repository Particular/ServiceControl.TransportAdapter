using System;
using System.Threading.Tasks;
using NServiceBus;
using ServiceControl.TransportAdapter;

namespace ServiceControl.RabbitMQ
{
    using TransportAdapter = TransportAdapter.TransportAdapter;

    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "ServiceControl.RabbitMQ";
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            var adapterConfig = new TransportAdapterConfig<RabbitMQTransport, MsmqTransport>("ServiceControl.RabbitMQ");
            adapterConfig.CustomizeEndpointTransport(InitializeTransport);
            var adapter = TransportAdapter.Create(adapterConfig);

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
