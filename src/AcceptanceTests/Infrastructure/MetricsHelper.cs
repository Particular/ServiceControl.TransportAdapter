using System.Linq;

static class MetricsHelper
{
    public static long MeterValue(this ScenarioContextWithMetrics context, string meterName)
    {
        var valueSource = context.MetricsData?.Meters.FirstOrDefault(m => m.Name == meterName);
        return valueSource?.Value.Count ?? -1;
    }
}