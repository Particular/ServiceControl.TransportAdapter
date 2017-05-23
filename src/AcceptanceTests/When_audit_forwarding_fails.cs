using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

[TestFixture]
public class When_audit_forwarding_fails : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_retries_forever()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AuditEndpoint>(c => c.When(s => s.SendLocal(new MyMessage())))
            .WithComponent(new AdapterComponent(c => c.ServiceControlSideAuditQueue = "InvalidAddress"))
            .Done(c => c.MeterValue("Audit forwarding failures") > 10)
            .Run();

        Assert.IsTrue(result.MeterValue("Audit forwarding failures") > 10);
    }

    class Context : ScenarioContextWithMetrics
    {
    }

    public class AuditEndpoint : EndpointConfigurationBuilder
    {
        public AuditEndpoint()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.HeartbeatPlugin("Particular.ServiceControl");
                c.AuditProcessedMessagesTo("Audit");
            });
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : IMessage
    {
    }
}