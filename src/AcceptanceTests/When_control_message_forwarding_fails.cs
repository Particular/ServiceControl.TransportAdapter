using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_control_message_forwarding_fails : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_drops_the_messages()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<HeartbeatingEndpoint>()
            .WithComponent(new AdapterComponent(c => c.ServiceControlSideControlQueue = "InvalidAddress"))
            .WithComponent(new ServiceControlFakeComponent<Context>(onControl: (m, c) =>
            {
                if (m.Headers[Headers.ReplyToAddress].Contains(Conventions.EndpointNamingConvention(typeof(HeartbeatingEndpoint))))
                {
                    c.ControlForwarded = true;
                }
            }))
            .Done(c => c.MeterValue("Control messages dropped") > 0)
            .Run();

        Assert.IsFalse(result.ControlForwarded);
        Assert.IsTrue(result.MeterValue("Control message forwarding failures") >= 4);
    }

    class Context : ScenarioContextWithMetrics
    {
        public bool ControlForwarded { get; set; }
    }

    public class HeartbeatingEndpoint : EndpointConfigurationBuilder
    {
        public HeartbeatingEndpoint()
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