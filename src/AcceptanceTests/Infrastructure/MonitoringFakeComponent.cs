using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Raw;

class MonitoringFakeComponent<TContext> : IComponentBehavior
    where TContext : ScenarioContext
{
    Action<string, Dictionary<string, string>, TContext> onReport;

    public MonitoringFakeComponent(Action<string, Dictionary<string, string>, TContext> onReport)
    {
        this.onReport = onReport;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var typedContext = (TContext)run.ScenarioContext;

        var config = RawEndpointConfiguration.Create("Particular.ServiceControl.Monitoring", (context, messages) =>
        {
            var bodyString = Encoding.UTF8.GetString(context.Body);
            onReport(bodyString, context.Headers, typedContext);
            return Task.CompletedTask;
        }, "poison");
        config.UseTransport<MsmqTransport>();
        config.AutoCreateQueue();

        return Task.FromResult<ComponentRunner>(new Runner(config));
    }

    class Runner : ComponentRunner
    {
        RawEndpointConfiguration configuration;
        IReceivingRawEndpoint endpoint;

        public Runner(RawEndpointConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public override async Task Start(CancellationToken token)
        {
            endpoint = await RawEndpoint.Start(configuration).ConfigureAwait(false);
        }

        public override Task Stop()
        {
            if (endpoint == null)
            {
                return Task.CompletedTask;
            }
            return endpoint.Stop();
        }

        public override string Name => "ServiceControl.Monitoring";
    }
}