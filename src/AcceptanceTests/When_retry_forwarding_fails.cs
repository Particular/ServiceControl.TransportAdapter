using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Faults;
using NUnit.Framework;

[TestFixture]
public class When_retry_forwarding_fails : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_should_return_message_back_to_SC()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<FaultyEndpoint>(c => c.When(s => s.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
            .WithComponent(new AdapterComponent())
            .WithComponent(new ServiceControlFakeComponent<Context>(onError: (m, c, sc) =>
            {
                if (c.RetryForwarded)
                {
                    c.RetryReturned = true;
                    c.ReturnedRetryHeaders = m.Headers;
                    return Task.CompletedTask;
                }
                m.Headers[FaultsHeaderKeys.FailedQ] = "InvalidAddress";
                c.RetryForwarded = true;
                return sc.Retry(m);
            }))
            .Done(c => c.RetryReturned)
            .Run();

        Assert.IsTrue(result.RetryForwarded);
        Assert.IsTrue(result.RetryReturned);

        Assert.AreEqual(1, result.MeterValue("Retry messages returned to ServiceControl"));
        Assert.AreEqual(4, result.MeterValue("Retry message forwarding failures"));
    }

    class Context : ScenarioContextWithMetrics
    {
        public bool RetryForwarded { get; set; }
        public bool RetryReturned { get; set; }
        public Dictionary<string, string> ReturnedRetryHeaders { get; set; }
    }

    public class FaultyEndpoint : EndpointConfigurationBuilder
    {
        public FaultyEndpoint()
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
                throw new SimulatedException("Boom!");
            }
        }
    }

    class MyMessage : IMessage
    {
    }
}