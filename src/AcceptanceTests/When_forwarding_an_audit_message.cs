using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_forwarding_an_audit_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_forwards_audit_messages()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AuditEndpoint>(c => c.When(s => s.SendLocal(new MyMessage())))
            .WithComponent(new AdapterComponent())
            .WithComponent(new ServiceControlFakeComponent<Context>(onAudit: (m, c) =>
            {
                c.AuditHeaders = m.Headers;
                c.AuditForwarded = true;
            }))
            .Done(c => c.AuditForwarded)
            .Run();

        Assert.IsTrue(result.AuditForwarded);
        StringAssert.StartsWith(Conventions.EndpointNamingConvention(typeof(AuditEndpoint)), result.AuditHeaders["_adapter.Original.ReplyToAddress"]);
        StringAssert.StartsWith(Conventions.EndpointNamingConvention(typeof(AuditEndpoint)), result.AuditHeaders[Headers.ReplyToAddress]);
    }

    class Context : ScenarioContext
    {
        public bool AuditForwarded { get; set; }
        public Dictionary<string, string> AuditHeaders { get; set; }
    }

    public class AuditEndpoint : EndpointConfigurationBuilder
    {
        public AuditEndpoint()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.SendFailedMessagesTo("error");
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