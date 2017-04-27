using System;
using System.Threading.Tasks;
using ConnectionManager;
using NServiceBus;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Routing;
using NServiceBus.Transport.SQLServer;
using ServiceControl.TransportAdapter;

namespace ServiceControl.SqlServer
{
    using TransportAdapter = TransportAdapter.TransportAdapter;

    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "ServiceControl.SqlServer";
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            var adapterConfig = new TransportAdapterConfig<SqlServerTransport, MsmqTransport>("ServiceControl.SqlServer");
            adapterConfig.CustomizeFrontendTransport(InitializeSqlTransport);

            adapterConfig.ConfigureIntegrationEventForwarding(
                new UnicastIntegrationEventPublishingStrategy("OtherEndpoint.IntegrationListener"),
                new UnicastIntegrationEventSubscribingStrategy());

            var adapter = TransportAdapter.Create(adapterConfig);

            await adapter.Start();

            Console.WriteLine("Press <enter> to shutdown adapter.");
            Console.ReadLine();

            await adapter.Stop();
        }

        static void InitializeSqlTransport(TransportExtensions<SqlServerTransport> transport)
        {
            transport.ConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=SCAdapter;Integrated Security=True");

            transport.EnableLegacyMultiInstanceMode(ConnectionFactory.GetConnection);
            //SQLServer expects this.
            transport.GetSettings().Set<EndpointInstances>(new EndpointInstances());
        }
    }
}
