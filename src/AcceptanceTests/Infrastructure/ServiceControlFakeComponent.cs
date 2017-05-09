using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using ServiceControl.TransportAdapter.AcceptanceTests.Infrastructure;

class ServiceControlFakeComponent<TContext> : IComponentBehavior, IServiceControl
    where TContext : ScenarioContext
{
    Action<IncomingMessage, TContext> onAudit;
    Func<IncomingMessage, TContext, IServiceControl, Task> onError;
    Action<IncomingMessage, TContext> onControl;
    ServiceControlFake<MsmqTransport> fake;

    public ServiceControlFakeComponent(Action<IncomingMessage, TContext> onAudit = null, Func<IncomingMessage, TContext, IServiceControl, Task> onError = null, Action<IncomingMessage, TContext> onControl = null)
    {
        this.onAudit = onAudit;
        this.onError = onError;
        this.onControl = onControl;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var typedContext = (TContext)run.ScenarioContext;

        fake = new ServiceControlFake<MsmqTransport>("Audit.Back", "Error.Back", "Particular.ServiceControl.Back", ex => { });
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
        ServiceControlFake<MsmqTransport> serviceControlFake;

        public Runner(ServiceControlFake<MsmqTransport> serviceControlFake)
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