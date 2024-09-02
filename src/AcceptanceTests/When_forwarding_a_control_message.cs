using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

[TestFixture]
public class When_forwarding_a_control_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_forwards_control_messages()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<HeartbeatingEndpoint>()
            .WithComponent(new AdapterComponent())
            .WithComponent(new ServiceControlFakeComponent<Context>(onControl: (m, c) => { c.ControlForwarded = true; }))
            .Done(c => c.ControlForwarded)
            .Run();

        Assert.That(result.ControlForwarded, Is.True);
    }

    class Context : ScenarioContext
    {
        public bool ControlForwarded { get; set; }
    }

    public class HeartbeatingEndpoint : EndpointConfigurationBuilder
    {
        public HeartbeatingEndpoint()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.SendFailedMessagesTo("error");
                c.SendHeartbeatTo("Particular.ServiceControl");
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