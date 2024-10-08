﻿using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_forwarding_a_failed_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_forwards_error_messages()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<FaultyEndpoint>(c => c.When(s => s.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
            .WithComponent(new AdapterComponent())
            .WithComponent(new ServiceControlFakeComponent<Context>(onError: (m, c, _) =>
            {
                c.ErrorForwarded = true;
                c.FailedMessageHeaders = m.Headers;
                return Task.CompletedTask;
            }))
            .Done(c => c.ErrorForwarded)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorForwarded, Is.True);
            Assert.That(result.FailedMessageHeaders["ServiceControl.RetryTo"], Is.EqualTo("Adapter.Retry"));
            Assert.That(result.FailedMessageHeaders["_adapter.Original.ReplyToAddress"], Does.StartWith(Conventions.EndpointNamingConvention(typeof(FaultyEndpoint))));
            Assert.That(result.FailedMessageHeaders[Headers.ReplyToAddress], Does.StartWith(Conventions.EndpointNamingConvention(typeof(FaultyEndpoint))));
        });
    }

    class Context : ScenarioContext
    {
        public bool ErrorForwarded { get; set; }
        public Dictionary<string, string> FailedMessageHeaders { get; set; }
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