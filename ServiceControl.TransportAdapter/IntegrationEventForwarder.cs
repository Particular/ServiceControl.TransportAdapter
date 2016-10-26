using System;
using System.Threading.Tasks;
using NServiceBus;

namespace ServiceControl.TransportAdapter
{
    class IntegrationEventForwarder : IHandleMessages<object>
    {
        FrontendPublisher publisher;

        public IntegrationEventForwarder(FrontendPublisher publisher)
        {
            this.publisher = publisher;
        }

        public Task Handle(object message, IMessageHandlerContext context)
        {
            Console.WriteLine($"Forwarding {message.GetType().Name} event.");
            return publisher.Publish(message);
        }
    }
}