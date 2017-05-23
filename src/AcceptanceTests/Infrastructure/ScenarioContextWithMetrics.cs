using Metrics.MetricData;
using NServiceBus.AcceptanceTesting;

public class ScenarioContextWithMetrics : ScenarioContext
{
    public MetricsData MetricsData { get; set; }
}