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

        Assert.Multiple(() =>
        {
            Assert.That(result.AuditForwarded, Is.True);
            Assert.That(result.AuditHeaders["_adapter.Original.ReplyToAddress"], Does.StartWith(Conventions.EndpointNamingConvention(typeof(AuditEndpoint))));
            Assert.That(result.AuditHeaders[Headers.ReplyToAddress], Does.StartWith(Conventions.EndpointNamingConvention(typeof(AuditEndpoint))));
        });
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
                c.AuditProcessedMessagesTo("audit");
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