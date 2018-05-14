using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using NUnit.Framework;
using ServiceControl.TransportAdapter.AcceptanceTests.Infrastructure;

class ServiceControlFakeComponent<TContext> : IComponentBehavior, IServiceControl
    where TContext : ScenarioContext
{
    Action<IncomingMessage, TContext> onAudit;
    Func<IncomingMessage, TContext, IServiceControl, Task> onError;
    Action<IncomingMessage, TContext> onControl;
    ServiceControlFake<LearningTransport> fake;

    public ServiceControlFakeComponent(Action<IncomingMessage, TContext> onAudit = null, Func<IncomingMessage, TContext, IServiceControl, Task> onError = null, Action<IncomingMessage, TContext> onControl = null)
    {
        this.onAudit = onAudit;
        this.onError = onError;
        this.onControl = onControl;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var typedContext = (TContext)run.ScenarioContext;

        fake = new ServiceControlFake<LearningTransport>("Audit.Back", "Error.Back", "Particular.ServiceControl.Back", ex =>
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
        });
        fake.ControlMessage += (sender, message) => onControl?.Invoke(message, typedContext);
        fake.MessageAudited += (sender, message) => onAudit?.Invoke(message, typedContext);
        fake.MessageFailed += (sender, message) => onError?.Invoke(message, typedContext, this);

        return Task.FromResult<ComponentRunner>(new Runner(fake));
    }

    public Task Retry(IncomingMessage message)
    {
        return fake.Retry(message);
    }

    class Runner : ComponentRunner
    {
        ServiceControlFake<LearningTransport> serviceControlFake;

        public Runner(ServiceControlFake<LearningTransport> serviceControlFake)
        {
            this.serviceControlFake = serviceControlFake;
        }

        public override Task Start(CancellationToken token)
        {
            return serviceControlFake.Start();
        }

        public override Task Stop()
        {
            //return Task.CompletedTask;
            return serviceControlFake.Stop();
        }

        public override string Name => "ServiceControl";
    }
}