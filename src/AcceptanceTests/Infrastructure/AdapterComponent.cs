using System;
using System.Threading;
using System.Threading.Tasks;
using Metrics;
using Metrics.MetricData;
using Metrics.Reporters;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using ServiceControl.TransportAdapter;

class AdapterComponent : IComponentBehavior
{
    Action<TransportAdapterConfig<MsmqTransport, MsmqTransport>> configAction;

    public AdapterComponent(Action<TransportAdapterConfig<MsmqTransport, MsmqTransport>> configAction = null)
    {
        this.configAction = configAction;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var config = new TransportAdapterConfig<MsmqTransport, MsmqTransport>("Adapter")
        {
            ServiceControlSideErrorQueue = "Error.Back",
            ServiceControlSideAuditQueue = "Audit.Back",
            ServiceControlSideControlQueue = "Particular.ServiceControl.Back"
        };

        var metricsContext = run.ScenarioContext as ScenarioContextWithMetrics;
        if (metricsContext != null)
        {
            config.MetricsConfig.WithReporting(r =>
            {
                r.WithReport(new ContextReport(metricsContext), TimeSpan.FromSeconds(1));
            });
        }

        configAction?.Invoke(config);

        var adapter = TransportAdapter.Create(config);

        return Task.FromResult<ComponentRunner>(new Runner(adapter));
    }

    class ContextReport : MetricsReport
    {
        ScenarioContextWithMetrics metricsContext;

        public ContextReport(ScenarioContextWithMetrics metricsContext)
        {
            this.metricsContext = metricsContext;
        }

        public void RunReport(MetricsData metricsData, Func<HealthStatus> healthStatus, CancellationToken token)
        {
            metricsContext.MetricsData = metricsData;
        }
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