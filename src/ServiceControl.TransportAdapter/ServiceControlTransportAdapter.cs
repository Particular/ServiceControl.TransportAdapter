using System.Threading.Tasks;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    class ServiceControlTransportAdapter<TFront, TBack> : ITransportAdapter
        where TFront : TransportDefinition, new()
        where TBack : TransportDefinition, new()
    {
        AuditForwarder<TFront, TBack> auditForwarder;
        FailedMessageForwarder<TFront, TBack> failedMessageForwarder;
        ControlForwarder<TFront, TBack> controlMessageForwarder;

        internal ServiceControlTransportAdapter(
            FailedMessageForwarder<TFront, TBack> failedMessageForwarder,
            ControlForwarder<TFront, TBack> controlMessageForwarder, 
            AuditForwarder<TFront, TBack> auditForwarder)
        {
            this.failedMessageForwarder = failedMessageForwarder;
            this.controlMessageForwarder = controlMessageForwarder;
            this.auditForwarder = auditForwarder;
        }

        public async Task Start()
        {
            await auditForwarder.Start().ConfigureAwait(false);
            await failedMessageForwarder.Start().ConfigureAwait(false);
            await controlMessageForwarder.Start().ConfigureAwait(false);
        }

        public async Task Stop()
        {
            await controlMessageForwarder.Stop().ConfigureAwait(false);
            await failedMessageForwarder.Stop().ConfigureAwait(false);
            await auditForwarder.Stop().ConfigureAwait(false);
        }
    }
}