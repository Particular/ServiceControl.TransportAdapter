namespace ServiceControl.TransportAdapter
{
    using System.Threading.Tasks;
    using NServiceBus.Transport;

    class ServiceControlTransportAdapter<TEndpoint, TServiceControl> : ITransportAdapter
        where TEndpoint : TransportDefinition, new()
        where TServiceControl : TransportDefinition, new()
    {
        internal ServiceControlTransportAdapter(
            FailedMessageForwarder<TEndpoint, TServiceControl> failedMessageForwarder,
            ControlForwarder<TEndpoint, TServiceControl> controlMessageForwarder,
            AuditForwarder<TEndpoint, TServiceControl> auditForwarder,
            NServiceBusMetricReport<TServiceControl> report)
        {
            this.failedMessageForwarder = failedMessageForwarder;
            this.controlMessageForwarder = controlMessageForwarder;
            this.auditForwarder = auditForwarder;
            this.report = report;
        }

        public async Task Start()
        {
            if (report != null)
            {
                await report.Start().ConfigureAwait(false);
            }
            await auditForwarder.Start().ConfigureAwait(false);
            await failedMessageForwarder.Start().ConfigureAwait(false);
            await controlMessageForwarder.Start().ConfigureAwait(false);
        }

        public async Task Stop()
        {
            await controlMessageForwarder.Stop().ConfigureAwait(false);
            await failedMessageForwarder.Stop().ConfigureAwait(false);
            await auditForwarder.Stop().ConfigureAwait(false);
            if (report != null)
            {
                await report.Stop().ConfigureAwait(false);
            }
        }

        AuditForwarder<TEndpoint, TServiceControl> auditForwarder;
        NServiceBusMetricReport<TServiceControl> report;
        FailedMessageForwarder<TEndpoint, TServiceControl> failedMessageForwarder;
        ControlForwarder<TEndpoint, TServiceControl> controlMessageForwarder;
    }
}