namespace ServiceControl.TransportAdapter.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using NUnit.Framework;

    [TestFixture]
    public class RetryForwarding : AcceptanceTestBase
    {
        [Test]
        public async Task It_forwards_retry_message()
        {
            var endpointConfig = new EndpointConfiguration("SCTA.RetryForwarding.Endpoint");
            endpointConfig.UseTransport<MsmqTransport>();
            endpointConfig.UsePersistence<InMemoryPersistence>();
            endpointConfig.ExcludeTypes();
            endpointConfig.DisableFeature<TimeoutManager>();
            endpointConfig.TypesToScanHack(this.GetNestedTypes());
            endpointConfig.SendFailedMessagesTo("SCTA.error-front");
            endpointConfig.EnableInstallers();

            var controller = new HandlerController()
            {
                HandlingMethod = m =>
                {
                    throw new Exception("Simulated");
                }
            };
            endpointConfig.RegisterComponents(c => c.ConfigureComponent(() => controller, DependencyLifecycle.SingleInstance));
            var endpoint = await Endpoint.Start(endpointConfig);

            var adapterConfig = PrepareAdapterConfig();
            var adapter = TransportAdapter.Create(adapterConfig);

            var scFake = PrepareServiceControlFake(e => { });
            var errorProcessedSignal = new TaskCompletionSource<IncomingMessage>();
            scFake.MessageFailed += (s, m) =>
            {
                errorProcessedSignal.SetResult(m);
            };

            await scFake.Start();
            await adapter.Start();
            await endpoint.SendLocal(new FaultyMessage());

            var message = await Wait(errorProcessedSignal.Task);

            Assert.IsTrue(message.Headers.ContainsKey("ServiceControl.RetryTo"));
            var retriedSignal = new TaskCompletionSource<FaultyMessage>();
            controller.HandlingMethod = m =>
            {
                retriedSignal.SetResult(m);
            };
            scFake.Retry(message);

            var retry = await Wait(retriedSignal.Task);
            Assert.Pass("Retry processed");
        }

        static async Task<T> Wait<T>(Task<T> completedSignal)
        {
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var finishedTask = await Task.WhenAny(completedSignal, timeoutTask).ConfigureAwait(false);
            if (finishedTask.Equals(timeoutTask))
            {
                Assert.Fail("Timeout");
            }
            return completedSignal.Result;
        }

        class FaultyMessage : IMessage
        {
        }

        class HandlerController
        {
            public Action<FaultyMessage> HandlingMethod { get; set; }
        }

        class FaultyMessageHandler : IHandleMessages<FaultyMessage>
        {
            public HandlerController Controller { get; set; }

            public Task Handle(FaultyMessage message, IMessageHandlerContext context)
            {
                Controller.HandlingMethod(message);
                return Task.CompletedTask;
            }
        }
        
    }
}