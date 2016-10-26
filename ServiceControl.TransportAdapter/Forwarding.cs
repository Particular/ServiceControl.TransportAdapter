using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    static class Forwarding
    {
        public static void EnableTwoWayDispatcherForwarding(EndpointConfiguration frontend, EndpointConfiguration backend)
        {
            var frontendDispatcherFuture = new TaskCompletionSource<IDispatchMessages>();
            var backendDispatcherFuture = new TaskCompletionSource<IDispatchMessages>();

            Enable(frontend, frontendDispatcherFuture, backendDispatcherFuture);
            Enable(backend, backendDispatcherFuture, frontendDispatcherFuture);
        }

        static void Enable(EndpointConfiguration source, TaskCompletionSource<IDispatchMessages> sourceFuture, TaskCompletionSource<IDispatchMessages> destinationFuture)
        {
            source.GetSettings().Set<DispatcherWrapper>(new DispatcherWrapper(sourceFuture));
            source.RegisterComponents(
                c =>
                {
                    c.ConfigureComponent(_ => new Forwarder(destinationFuture.Task), DependencyLifecycle.SingleInstance);
                });
            source.EnableFeature<DispatcherForwarderFeature>();
        }
    }
}