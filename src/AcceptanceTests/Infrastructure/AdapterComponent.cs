using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using ServiceControl.TransportAdapter;

class AdapterComponent : IComponentBehavior
{
    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var config = new TransportAdapterConfig<MsmqTransport, MsmqTransport>("Adapter")
        {
            BackendErrorQueue = "Error.Back",
            BackendAuditQueue = "Audit.Back",
            BackendServiceControlQueue = "Particular.ServiceControl.Back"
        };

        var adapter = TransportAdapter.Create(config);

        return Task.FromResult<ComponentRunner>(new Runner(adapter));
    }

    class Runner : ComponentRunner
    {
        ServiceControlTransportAdapter<MsmqTransport, MsmqTransport> adapter;

        public Runner(ServiceControlTransportAdapter<MsmqTransport, MsmqTransport> adapter)
        {
            this.adapter = adapter;
        }

        public override Task Start(CancellationToken token)
        {
            return adapter.Start();
        }

        public override Task Stop()
        {
            //return Task.CompletedTask;
            return adapter.Stop();
        }

        public override string Name => "ServiceControl";
    }
}