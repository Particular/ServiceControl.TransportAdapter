using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using ConnectionManager;
using NServiceBus;
using NServiceBus.Transport.SQLServer;

namespace SomeEndpoint
{
    class Program
    {
        static void Main(string[] args)
        {
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            var config = new EndpointConfiguration("SomeEndpoint");

            var transport = config.UseTransport<SqlServerTransport>();
            transport.ConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=SCAdapter_Some;Integrated Security=True");
            transport.EnableLegacyMultiInstanceMode(ConnectionFactory.GetConnection);

            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("SCAdapter.error");
            config.AuditProcessedMessagesTo("SCAdapter.audit");
            config.EnableInstallers();
            config.Recoverability().Immediate(i => i.NumberOfRetries(0));
            config.Recoverability().Delayed(d => d.NumberOfRetries(0));

            var endpoint = await Endpoint.Start(config);
            
            Console.WriteLine("Press <enter> to send a message the endpoint.");

            while (true)
            {
                Console.ReadLine();
                await endpoint.SendLocal(new SomeMessage());
            }
        }
    }

    class SomeMessageHandler : IHandleMessages<SomeMessage>
    {
        static readonly Random R = new Random();
        public Task Handle(SomeMessage message, IMessageHandlerContext context)
        {
            if (R.Next(2) == 0)
            {
                throw new Exception("Simulated");
            }
            Console.WriteLine("Processed");
            return Task.FromResult(0);
        }
    }

    class SomeMessage : IMessage
    {
    }
}
