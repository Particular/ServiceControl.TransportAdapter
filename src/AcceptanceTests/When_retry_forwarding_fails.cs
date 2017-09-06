using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Faults;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

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
                m.Headers["ServiceControl.TargetEndpointAddress"] = "InvalidAddress";
                c.RetryForwarded = true;
                return sc.Retry(m);
            }))
            .Done(c => c.RetryReturned)
            .Run();

        Assert.IsTrue(result.RetryForwarded);
        Assert.IsTrue(result.RetryReturned);

        StringAssert.StartsWith(Conventions.EndpointNamingConvention(typeof(FaultyEndpoint)), result.ReturnedRetryHeaders[FaultsHeaderKeys.FailedQ]);
    }

    class Context : ScenarioContext
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
                c.SendFailedMessagesTo("error");
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