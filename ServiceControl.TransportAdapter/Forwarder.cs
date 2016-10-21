using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    class Forwarder
    {
        IDispatchMessages dispatcher;
        public void Initialize(IDispatchMessages dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public Task Forward(string destination, string messageId, Dictionary<string, string> headers, byte[] body, TransportTransaction transportTransaction)
        {
            var outgoingMessage = new OutgoingMessage(messageId, headers, body);
            var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destination));
            return dispatcher.Dispatch(new TransportOperations(operation), transportTransaction, new ContextBag());
        }
    }
}