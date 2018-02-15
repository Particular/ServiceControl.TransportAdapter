namespace ServiceControl.TransportAdapter.Tests
{
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    class FakeTransport : TransportDefinition
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            throw new System.NotImplementedException();
        }

        public override string ExampleConnectionStringForErrorMessage { get; }
    }
}