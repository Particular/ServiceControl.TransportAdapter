using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

[TestFixture]
public class ControlForwarding : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_forwards_control_messages()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AuditEndpoint>()
            .WithComponent(new AdapterComponent())
            .WithComponent(new ServiceControlFakeComponent<Context>(onControl: (m, c) => { c.ControlForwarded = true; }))
            .Done(c => c.ControlForwarded)
            .Run();

        Assert.IsTrue(result.ControlForwarded);
    }

    class Context : ScenarioContext
    {
        public bool ControlForwarded { get; set; }
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