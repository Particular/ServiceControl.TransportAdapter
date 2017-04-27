using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

[TestFixture]
public class ErrorForwarding : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_forwards_error_messages()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AuditEndpoint>(c => c.When(s => s.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
            .WithComponent(new AdapterComponent())
            .WithComponent(new ServiceControlFakeComponent<Context>(onError: (m, c, _) =>
            {
                c.ErrorForwarded = true;
                return Task.CompletedTask;
            }))
            .Done(c => c.ErrorForwarded)
            .Run();

        Assert.IsTrue(result.ErrorForwarded);
    }

    class Context : ScenarioContext
    {
        public bool ErrorForwarded { get; set; }
    }

    public class AuditEndpoint : EndpointConfigurationBuilder
    {
        public AuditEndpoint()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.HeartbeatPlugin("Particular.ServiceControl");
            });
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                throw new SimulatedException("Boom!");
            }
        }
    }

    class MyMessage : IMessage
    {
    }
}