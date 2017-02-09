namespace ServiceControl.TransportAdapter.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using NUnit.Framework;

    [TestFixture]
    public class ErrorForwarding : AcceptanceTestBase
    {
        [Test]
        public async Task It_forwards_failed_message()
        {
            var endpointConfig = new EndpointConfiguration("SCTA.ErrorForwarding.Endpoint");
            endpointConfig.UseTransport<MsmqTransport>();
            endpointConfig.UsePersistence<InMemoryPersistence>();
            endpointConfig.ExcludeTypes();
            endpointConfig.DisableFeature<TimeoutManager>();
            endpointConfig.TypesToScanHack(this.GetNestedTypes());
            endpointConfig.SendFailedMessagesTo("SCTA.error-front");
            endpointConfig.EnableInstallers();

            var endpoint = await Endpoint.Start(endpointConfig);

            var adapterConfig = PrepareAdapterConfig();
            var adapter = TransportAdapter.Create(adapterConfig);

            var scFake = PrepareServiceControlFake(e => { });
            var completedSignal = new TaskCompletionSource<IncomingMessage>();
            scFake.MessageFailed += (s, m) =>
            {
                completedSignal.SetResult(m);
            };

            await scFake.Start();
            await adapter.Start();
            await endpoint.SendLocal(new FaultyMessage());

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var finishedTask = await Task.WhenAny(completedSignal.Task, timeoutTask).ConfigureAwait(false);
            if (finishedTask.Equals(timeoutTask))
            {
                Assert.Fail("Timeout");
            }
            var message = completedSignal.Task.Result;
            Assert.IsTrue(message.Headers.ContainsKey("ServiceControl.RetryTo"));
        }

        class FaultyMessage : IMessage
        {
        }

        class FaultyMessageHandler : IHandleMessages<FaultyMessage>
        {
            public Task Handle(FaultyMessage message, IMessageHandlerContext context)
            {
                throw new Exception("Simulated");
            }
        }
        
    }
}