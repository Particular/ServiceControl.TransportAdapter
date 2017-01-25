namespace ServiceControl.TransportAdapter.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using NUnit.Framework;

    [TestFixture]
    public class AuditForwarding : AcceptanceTestBase
    {
        [Test]
        public async Task It_forwards_processed_message()
        {
            var endpointConfig = new EndpointConfiguration("SCTA.ErrorForwarding.Endpoint");
            endpointConfig.UseTransport<MsmqTransport>();
            endpointConfig.UsePersistence<InMemoryPersistence>();
            endpointConfig.ExcludeTypes();
            endpointConfig.DisableFeature<TimeoutManager>();
            endpointConfig.TypesToScanHack(this.GetNestedTypes());
            endpointConfig.SendFailedMessagesTo("SCTA.error-front");
            endpointConfig.AuditProcessedMessagesTo("SCTA.audit-front");
            endpointConfig.EnableInstallers();

            var endpoint = await Endpoint.Start(endpointConfig);

            var adapterConfig = PrepareAdapterConfig();
            var adapter = TransportAdapter.Create(adapterConfig);

            var scFake = PrepareServiceControlFake(e => { });
            var completedSignal = new TaskCompletionSource<IncomingMessage>();
            scFake.MessageAudited += (s, m) =>
            {
                completedSignal.SetResult(m);
            };

            await scFake.Start();
            await adapter.Start();
            await endpoint.SendLocal(new MyMessage());

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var finishedTask = await Task.WhenAny(completedSignal.Task, timeoutTask).ConfigureAwait(false);
            if (finishedTask.Equals(timeoutTask))
            {
                Assert.Fail("Timeout");
            }
            var message = completedSignal.Task.Result;
            Assert.IsTrue(message.Headers.ContainsKey(Headers.ProcessingEndpoint));
        }

        class MyMessage : IMessage
        {
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                return Task.CompletedTask;
            }
        }
        
    }
}