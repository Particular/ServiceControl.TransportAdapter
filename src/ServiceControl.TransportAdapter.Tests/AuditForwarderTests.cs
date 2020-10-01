namespace ServiceControl.TransportAdapter.Tests
{
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture]
    public class AuditForwarderTests
    {
        [Test]
        public void Should_apply_standard_config_before_customizations()
        {
            const string CreateQueuesKey = "NServiceBus.Raw.CreateQueue";

            SettingsHolder frontEndSettings = null, backendSettings = null;
            var auditForwarder = new AuditForwarder<FakeTransport, FakeTransport>("adapterName", "frontendAuditQueue", "backendAuditQueue", "poisonMessageQueue", frontend =>
            {
                frontEndSettings = frontend.GetSettings();
                frontEndSettings.Set(CreateQueuesKey, false);
            }, backend =>
            {
                backendSettings = backend.GetSettings();
                backendSettings.Set(CreateQueuesKey, false);
            });

            Assert.IsFalse(frontEndSettings.Get<bool>(CreateQueuesKey));
            Assert.IsFalse(backendSettings.Get<bool>(CreateQueuesKey));
        }
    }
}