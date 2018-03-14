using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;
using ServiceControl.TransportAdapter;

class AdapterComponent : IComponentBehavior
{
    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var config = new TransportAdapterConfig<LearningTransport, LearningTransport>("Adapter")
        {
            ServiceControlSideErrorQueue = "Error.Back",
            ServiceControlSideAuditQueue = "Audit.Back",
            ServiceControlSideControlQueue = "Particular.ServiceControl.Back"
        };
        config.CustomizeEndpointTransport(CustomizeTransport);
        config.CustomizeServiceControlTransport(CustomizeTransport);
        var adapter = TransportAdapter.Create(config);

        return Task.FromResult<ComponentRunner>(new Runner(adapter));
    }

    void CustomizeTransport(TransportExtensions<LearningTransport> ex)
    {
        var testRunId = TestContext.CurrentContext.Test.ID;
        string tempDir;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            //can't use bin dir since that will be too long on the build agents
            tempDir = @"c:\temp";
        }
        else
        {
            tempDir = Path.GetTempPath();
        }

        var storageDir = Path.Combine(tempDir, testRunId);
        ex.StorageDirectory(storageDir);
    }

    class Runner : ComponentRunner
    {
        ITransportAdapter adapter;

        public Runner(ITransportAdapter adapter)
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