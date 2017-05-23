using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Metrics;
using Metrics.Json;
using Metrics.MetricData;
using Metrics.Reporters;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Routing;
using NServiceBus.Support;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    using NServiceBus.Raw;

    class NServiceBusMetricReport<TServiceControl> : MetricsReport
        where TServiceControl : TransportDefinition, new()
    {
        public NServiceBusMetricReport(string name, Guid hostId, string serviceControlMonitoringQueue,
            Action<TransportExtensions<TServiceControl>> backendTransportCustomization)
        {
            senderConfig = RawEndpointConfiguration.CreateSendOnly($"{name}.MetricsSender");
            var transport = senderConfig.UseTransport<TServiceControl>();
            backendTransportCustomization(transport);

            destination = new UnicastAddressTag(serviceControlMonitoringQueue);

            headers["NServiceBus.Monitoring.NodeType"] = "ServiceControl.TransportAdapter";
            headers[Headers.OriginatingMachine] = RuntimeEnvironment.MachineName;
            headers[Headers.OriginatingHostId] = hostId.ToString("N");
            headers[Headers.EnclosedMessageTypes] = "NServiceBus.Metrics.MetricReport"; // without assembly name to allow ducktyping
            headers[Headers.ContentType] = ContentTypes.Json;
        }

        public async Task Start()
        {
            dispatcher = await RawEndpoint.Start(senderConfig).ConfigureAwait(false);
        }

        public Task Stop()
        {
            var sender = dispatcher;
            dispatcher = null;
            return sender.Stop();
        }

        public void RunReport(MetricsData metricsData, Func<HealthStatus> healthStatus, CancellationToken token)
        {
            if (dispatcher == null)
            {
                return;
            }
            RunReportAsync(metricsData)
                .IgnoreContinuation();
        }

        async Task RunReportAsync(MetricsData metricsData)
        {
            var stringBody = $@"{{""Data"" : {JsonBuilderV2.BuildJson(metricsData)}}}";
            var body = Encoding.UTF8.GetBytes(stringBody);

            headers[Headers.OriginatingEndpoint] = metricsData.Context; // assumption that it will be always the endpoint name
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, body);
            var operation = new TransportOperation(message, destination);

            try
            {
                await dispatcher.Dispatch(new TransportOperations(operation), transportTransaction, new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                log.Error($"Error while sending metric data to {destination}.", exception);
            }
        }

        UnicastAddressTag destination;
        volatile IReceivingRawEndpoint dispatcher;
        TransportTransaction transportTransaction = new TransportTransaction();

        Dictionary<string, string> headers = new Dictionary<string, string>();

        static ILog log = LogManager.GetLogger<NServiceBusMetricReport<TServiceControl>>();
        RawEndpointConfiguration senderConfig;
    }
}