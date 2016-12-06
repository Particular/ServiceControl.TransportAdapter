using System;
using System.Threading.Tasks;
using Metrics;
using NServiceBus;
using ServiceControl.TransportAdapter;

namespace SqlServerThroughputTest
{
    class Program
    {
        static void Main(string[] args)
        {
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            var senderConfig = new EndpointConfiguration("Sender");
            senderConfig.SendOnly();
            var transport = senderConfig.UseTransport<SqlServerTransport>();
            transport.ConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=SCAdapter_PerfTest;Integrated Security=True");
            var routing = transport.Routing();
            routing.RouteToEndpoint(typeof(MyMessage), "ServiceControl.SqlServer.audit");
            senderConfig.SendFailedMessagesTo("error");
            senderConfig.UsePersistence<InMemoryPersistence>();

            var sender = await Endpoint.Start(senderConfig);

            for (var i = 0; i < 30000; i++)
            {
                await sender.Send(new MyMessage());
            }
            await sender.Stop();

            Console.WriteLine("Messages ready. Press <enter> to start adapter.");
            Console.ReadLine();

            Metric.Config
                .WithReporting(r =>
                {
                    r.WithConsoleReport(TimeSpan.FromSeconds(1));
                });

            var adapter = new ServiceControlTransportAdapter<SqlServerTransport, MsmqTransport>("ServiceControl.SqlServer", new string[0], InitializeSqlTransport);

            await adapter.Start();

            Console.WriteLine("Press <enter> to shutdown adapter.");
            Console.ReadLine();

            await adapter.Stop();
        }

        static void InitializeSqlTransport(TransportExtensions<SqlServerTransport> transport)
        {
            transport.ConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=SCAdapter_PerfTest;Integrated Security=True");
        }
    }

    class MyMessage : IMessage
    {
    }
}
