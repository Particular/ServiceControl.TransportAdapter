using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

[TestFixture]
public class RetryForwarding : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_forwards_retry_messages()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AuditEndpoint>(c => c.When(s => s.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
            .WithComponent(new AdapterComponent())
            .WithComponent(new ServiceControlFakeComponent<Context>(onError: (m, c, sc) =>
            {
                m.Headers["IsRetry"] = "true";
                return sc.Retry(m);
            }))
            .Done(c => c.RetryForwarded)
            .Run();

        Assert.IsTrue(result.RetryForwarded);
    }

    class Context : ScenarioContext
    {
        public bool RetryForwarded { get; set; }
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
            public Context Context { get; set; }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                if (context.MessageHeaders.ContainsKey("IsRetry"))
                {
                    Context.RetryForwarded = true;
                    return Task.CompletedTask;
                }
                throw new SimulatedException("Boom!");
            }
        }
    }

    class MyMessage : IMessage
    {
    }
}