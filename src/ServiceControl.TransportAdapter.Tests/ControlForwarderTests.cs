namespace ServiceControl.TransportAdapter.Tests
{
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture]
    public class ControlForwarderTests
    {
        [Test]
        public void Should_apply_standard_config_before_customizations()
        {
            const string CreateQueuesKey = "NServiceBus.Raw.CreateQueue";

            SettingsHolder frontEndSettings = null, backendSettings = null;
            // ReSharper disable once UnusedVariable
            var controlForwarder = new ControlForwarder<FakeTransport, FakeTransport>("adapterName", "frontendControlQueue", "backendControlQueue", "poisonMessageQueue", frontend =>
            {
                frontEndSettings = frontend.GetSettings();
                frontEndSettings.Set(CreateQueuesKey, false);
            }, backend =>
            {
                backendSettings = backend.GetSettings();
                backendSettings.Set(CreateQueuesKey, false);
            }, 0);

            Assert.IsFalse(frontEndSettings.Get<bool>(CreateQueuesKey));
            Assert.IsFalse(backendSettings.Get<bool>(CreateQueuesKey));
        }
    }
}