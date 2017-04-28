namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NServiceBus.Unicast.Queuing;
    using NServiceBus.Unicast.Transport;

    public class UnicastIntegrationEventSubscribingStrategy : IIntegrationEventSubscribingStrategy
    {
        public Task EnsureSubscribed(IRawEndpoint integrationEventSubscriber, string serviceControlInputQueue)
        {
            var subscribeTasks = messageTypes.Select(t => SendMessage(integrationEventSubscriber, t, serviceControlInputQueue, new ContextBag(), MessageIntentEnum.Subscribe));
            return Task.WhenAll(subscribeTasks.ToArray());
        }

        static Task SendMessage(IRawEndpoint endpoint, string eventTypeAssemblyQualifiedName, string publisherAddress, ContextBag context, MessageIntentEnum intent)
        {
            var subscriptionMessage = ControlMessageFactory.Create(intent);

            subscriptionMessage.Headers[Headers.SubscriptionMessageType] = eventTypeAssemblyQualifiedName;
            subscriptionMessage.Headers[Headers.ReplyToAddress] = endpoint.TransportAddress;
            subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = endpoint.TransportAddress;
            subscriptionMessage.Headers[Headers.SubscriberEndpoint] = endpoint.EndpointName;
            subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
            subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.1.0"; //We pretend to be 6.1.0.

            return TrySendMessage(endpoint, publisherAddress, subscriptionMessage, eventTypeAssemblyQualifiedName, context);
        }

        static async Task TrySendMessage(IDispatchMessages endpoint, string destination, OutgoingMessage subscriptionMessage, string messageType, ContextBag context)
        {
            try
            {
                var transportOperation = new TransportOperation(subscriptionMessage, new UnicastAddressTag(destination));
                var transportTransaction = context.GetOrCreate<TransportTransaction>();
                await endpoint.Dispatch(new TransportOperations(transportOperation), transportTransaction, context).ConfigureAwait(false);
            }
            catch (QueueNotFoundException ex)
            {
                string message = $"Failed to subscribe to {messageType} at publisher queue {destination}, reason {ex.Message}";
                Logger.Error(message, ex);
                throw new QueueNotFoundException(destination, message, ex);
            }
        }

        static string[] messageTypes =
        {
            typeof(MessageFailed).AssemblyQualifiedName,
            typeof(CustomCheckFailed).AssemblyQualifiedName,
            typeof(CustomCheckSucceeded).AssemblyQualifiedName,
            typeof(HeartbeatRestored).AssemblyQualifiedName,
            typeof(HeartbeatStopped).AssemblyQualifiedName
        };

        static ILog Logger = LogManager.GetLogger(typeof(UnicastIntegrationEventSubscribingStrategy));
    }
}