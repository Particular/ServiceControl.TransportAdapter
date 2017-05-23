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
        ContextReport contextReport = null;
        var metricsContext = run.ScenarioContext as ScenarioContextWithMetrics;
        if (metricsContext != null)
        {
            config.MetricsConfig.WithReporting(r =>
            {
                contextReport = new ContextReport(metricsContext);
                r.WithReport(contextReport, TimeSpan.FromSeconds(1));
            });
        }

        configAction?.Invoke(config);

        var adapter = TransportAdapter.Create(config);

        return Task.FromResult<ComponentRunner>(new Runner(adapter, contextReport));
    }

    class ContextReport : MetricsReport
    {
        ScenarioContextWithMetrics metricsContext;
        TaskCompletionSource<bool> nextReport;

        public ContextReport(ScenarioContextWithMetrics metricsContext)
        {
            this.metricsContext = metricsContext;
        }

        public void RunReport(MetricsData metricsData, Func<HealthStatus> healthStatus, CancellationToken token)
        {
            metricsContext.MetricsData = metricsData;
            nextReport?.SetResult(true);
        }

        public Task<bool> WaitForNextReport()
        {
            nextReport = new TaskCompletionSource<bool>();
            return nextReport.Task;
        }
    }

    class Runner : ComponentRunner
    {
        ITransportAdapter adapter;
        ContextReport contextReport;

        public Runner(ITransportAdapter adapter, ContextReport contextReport)
        {
            this.adapter = adapter;
            this.contextReport = contextReport;
        }

        public override Task Start(CancellationToken token)
        {
            return adapter.Start();
        }

        public override async Task Stop()
        {
            if (contextReport != null)
            {
                await contextReport.WaitForNextReport().ConfigureAwait(false);
            }
            await adapter.Stop().ConfigureAwait(false);
        }

        public override string Name => "ServiceControl";
    }
}