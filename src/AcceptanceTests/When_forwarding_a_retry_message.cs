using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_forwarding_a_retry_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_forwards_retry_messages()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<FautyEndpoint>(c => c.When(s => s.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
            .WithComponent(new AdapterComponent())
            .WithComponent(new ServiceControlFakeComponent<Context>(onError: (m, c, sc) =>
            {
                m.Headers["IsRetry"] = "true";
                return sc.Retry(m);
            }))
            .Done(c => c.RetryForwarded)
            .Run();

        Assert.That(result.RetryForwarded, Is.True);
        Assert.That(result.RetryHeaders[Headers.ReplyToAddress], Does.StartWith(Conventions.EndpointNamingConvention(typeof(FautyEndpoint))));
    }

    class Context : ScenarioContext
    {
        public bool RetryForwarded { get; set; }
        public IReadOnlyDictionary<string, string> RetryHeaders { get; set; }
    }

    public class FautyEndpoint : EndpointConfigurationBuilder
    {
        public FautyEndpoint()
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
                if (context.MessageHeaders.ContainsKey("IsRetry"))
                {
                    Context.RetryHeaders = context.MessageHeaders;
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