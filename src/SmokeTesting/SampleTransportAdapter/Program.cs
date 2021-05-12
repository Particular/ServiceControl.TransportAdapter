using System;
using System.Threading.Tasks;
using NServiceBus;
using ServiceControl.TransportAdapter;

class Program
{
    static async Task Main()
    {
        Console.Title = "Transport Adapter";

        var config = new TransportAdapterConfig<SqlServerTransport, MsmqTransport>("Adapter");

        config.CustomizeEndpointTransport(endpoints =>
        {
            endpoints.ConnectionString(
                @"Server=.\SQLEXPRESS;Database=NServiceBus;Integrated Security=SSPI;Max Pool Size=100");
        });

        var adapter = TransportAdapter.Create(config);

        await adapter.Start().ConfigureAwait(false);

        while (Console.ReadKey(true).Key != ConsoleKey.Escape)
        {
        }

        await adapter.Stop().ConfigureAwait(false);
    }
}

