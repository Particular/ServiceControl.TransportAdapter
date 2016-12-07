using System;
using System.Threading.Tasks;
using ConnectionManager;
using NServiceBus;
using NServiceBus.Transport.SQLServer;
using ServiceControl.TransportAdapter;

namespace ServiceControl.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            var adapter = new ServiceControlTransportAdapter<SqlServerTransport, MsmqTransport>("ServiceControl.SqlServer", 
                new UnicastIntegrationEventPublishingStrategy("SomeEndpoint", "YetAnotherEndpoint"), 
                InitializeSqlTransport);

            await adapter.Start();

            Console.WriteLine("Press <enter> to shutdown adapter.");
            Console.ReadLine();

            await adapter.Stop();
        }

        static void InitializeSqlTransport(TransportExtensions<SqlServerTransport> transport)
        {
            transport.ConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=SCAdapter;Integrated Security=True");
            transport.EnableLegacyMultiInstanceMode(ConnectionFactory.GetConnection);
        }
    }
}
