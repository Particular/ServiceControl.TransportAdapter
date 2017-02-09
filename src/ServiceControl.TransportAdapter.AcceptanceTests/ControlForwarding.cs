namespace ServiceControl.TransportAdapter.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Features;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using NUnit.Framework;

    [TestFixture]
    public class ControlForwarding : AcceptanceTestBase
    {
        [Test]
        public async Task It_forwards_control_message()
        {
            var endpointConfig = new EndpointConfiguration("SCTA.ControlForwarding.Endpoint");
            endpointConfig.UseTransport<MsmqTransport>();
            endpointConfig.UsePersistence<InMemoryPersistence>();
            endpointConfig.ExcludeTypes();
            endpointConfig.DisableFeature<TimeoutManager>();
            endpointConfig.TypesToScanHack(this.GetNestedTypes().Concat(new[] {typeof(Heartbeats) }));
            endpointConfig.SendFailedMessagesTo("SCTA.error-front");
            endpointConfig.HeartbeatPlugin("SCTA.control-front");
            endpointConfig.EnableInstallers();


            var adapterConfig = PrepareAdapterConfig();
            var adapter = TransportAdapter.Create(adapterConfig);

            var scFake = PrepareServiceControlFake(e => { });
            var completedSignal = new TaskCompletionSource<IncomingMessage>();
            scFake.ControlMessage += (s, m) =>
            {
                completedSignal.SetResult(m);
            };

            await scFake.Start();
            await adapter.Start();

            //HB message is sent on start
            // ReSharper disable once UnusedVariable
            var endpoint = await Endpoint.Start(endpointConfig);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var finishedTask = await Task.WhenAny(completedSignal.Task, timeoutTask).ConfigureAwait(false);
            if (finishedTask.Equals(timeoutTask))
            {
                Assert.Fail("Timeout");
            }
            var message = completedSignal.Task.Result;
            var enclosedTypes = message.Headers[Headers.EnclosedMessageTypes];

            Assert.IsTrue(enclosedTypes.Contains("RegisterEndpointStartup"));
        }
    }
}