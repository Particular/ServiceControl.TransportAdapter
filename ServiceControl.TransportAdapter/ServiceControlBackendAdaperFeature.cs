using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.ConsistencyGuarantees;
using NServiceBus.Features;
using NServiceBus.ObjectBuilder;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    class ServiceControlBackendAdaperFeature : Feature
    {
        const string TargetAddressHeader = "ServiceControl.TargetEndpointAddress";

        protected override void Setup(FeatureConfigurationContext context)
        {
            var requiredTransactionSupport = context.Settings.GetRequiredTransactionModeForReceives();

            context.AddSatelliteReceiver("ForwardRetries", GetSatelliteAddress(context, "retry"), requiredTransactionSupport, new PushRuntimeSettings(), HandleFailure, ForwardRetry);
        }

        static string GetSatelliteAddress(FeatureConfigurationContext context, string suffix)
        {
            var satelliteLogicalAddress = context.Settings.LogicalAddress().CreateQualifiedAddress(suffix);
            var satelliteAddress = context.Settings.GetTransportAddress(satelliteLogicalAddress);
            return satelliteAddress;
        }

        RecoverabilityAction HandleFailure(RecoverabilityConfig config, ErrorContext context)
        {
            return RecoverabilityAction.ImmediateRetry(); //For now
        }

        static Task ForwardRetry(IBuilder builder, MessageContext context)
        {
            var forwarder = builder.Build<Forwarder>();

            var destination = context.Headers[TargetAddressHeader];

            Console.WriteLine($"Forwarding a retry message to {destination}");

            context.Headers.Remove(TargetAddressHeader);

            return forwarder.Forward(destination, context.MessageId, context.Headers, context.Body, context.TransportTransaction);
        }
    }
}