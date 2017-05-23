using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NUnit.Framework;

[TestFixture]
public class When_sending_metrics_to_ServiceControl : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Report_contains_all_metrics()
    {
        var result = await Scenario.Define<Context>()
            .WithComponent(new AdapterComponent(c => c.SendMetricDataToServiceControl(null, TimeSpan.FromSeconds(1))))
            .WithComponent(new MonitoringFakeComponent<Context>((b, h, c) => { c.Report = b; }))
            .Done(c => c.Report != null)
            .Run();

        Assert.IsNotNull(result.Report);
        StringAssert.Contains("Audits forwarded", result.Report);
        StringAssert.Contains("Audit forwarding failures", result.Report);

        StringAssert.Contains("Errors forwarded", result.Report);
        StringAssert.Contains("Error forwarding failures", result.Report);

        StringAssert.Contains("Control messages forwarded", result.Report);
        StringAssert.Contains("Control messages dropped", result.Report);
        StringAssert.Contains("Control message forwarding failures", result.Report);

        StringAssert.Contains("Retry messages forwarded", result.Report);
        StringAssert.Contains("Retry message forwarding failures", result.Report);
        StringAssert.Contains("Retry messages returned to ServiceControl", result.Report);
    }

    class Context : ScenarioContext
    {
        public string Report { get; set; }
    }

    class MyMessage : IMessage
    {
    }
}