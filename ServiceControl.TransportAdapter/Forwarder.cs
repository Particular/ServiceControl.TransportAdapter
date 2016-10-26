using System.Collections.Generic;
using System.Threading.Tasks;
using Metrics;
using NServiceBus.Extensibility;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    class Forwarder
    {
        Task<IDispatchMessages> dispatcherFuture;

        public Forwarder(Task<IDispatchMessages> dispatcherFuture)
        {
            this.dispatcherFuture = dispatcherFuture;
        }

        public async Task Forward(string destination, string messageId, Dictionary<string, string> headers, byte[] body, TransportTransaction transportTransaction)
        {
            var dispatcher = await dispatcherFuture.ConfigureAwait(false);
            var outgoingMessage = new OutgoingMessage(messageId, headers, body);
            var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destination));
            await dispatcher.Dispatch(new TransportOperations(operation), transportTransaction, new ContextBag());
        }
    }
}