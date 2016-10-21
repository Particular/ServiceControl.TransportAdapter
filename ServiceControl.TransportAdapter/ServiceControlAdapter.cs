using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Features;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    public class ServiceControlAdapter<TFrontendTransport>
        where TFrontendTransport : TransportDefinition, new()
    {
        EndpointConfiguration fronendConfig;
        EndpointConfiguration backendConfig;
        IEndpointInstance frontend;
        IEndpointInstance backend;
        DispatcherWrapper frontendDispatcherWrapper;
        DispatcherWrapper backednDispatcherWrapper;
        Forwarder fronendForwarder;
        Forwarder backendForwarder;

        public ServiceControlAdapter(string name, Action<EndpointConfiguration, TransportExtensions<TFrontendTransport>> provideFrontendConfiguration)
        {
            fronendConfig = new EndpointConfiguration(name);
            var transport = fronendConfig.UseTransport<TFrontendTransport>();
            fronendConfig.UsePersistence<InMemoryPersistence>();
            fronendConfig.SendFailedMessagesTo("error"); //Not used
            fronendConfig.EnableFeature<ServiceControlFrontendAdapterFeature>();
            frontendDispatcherWrapper = new DispatcherWrapper();
            fronendConfig.GetSettings().Set<DispatcherWrapper>(frontendDispatcherWrapper);
            fronendConfig.DisableFeature<TimeoutManager>();
            fronendForwarder = new Forwarder();
            fronendConfig.RegisterComponents(c =>
            {
                c.ConfigureComponent(_ => fronendForwarder, DependencyLifecycle.SingleInstance);
            });
            provideFrontendConfiguration(fronendConfig, transport);

            backendConfig = new EndpointConfiguration(name);
            backendConfig.UseTransport<MsmqTransport>(); //Always use MSMQ on the backend
            backendConfig.UsePersistence<InMemoryPersistence>();
            backendConfig.DisableFeature<TimeoutManager>();
            backendConfig.SendFailedMessagesTo("error"); //Not used because the main queue is not used.
            backendConfig.EnableFeature<ServiceControlBackendAdaperFeature>();
            backednDispatcherWrapper = new DispatcherWrapper();
            backendConfig.GetSettings().Set<DispatcherWrapper>(backednDispatcherWrapper);
            backendForwarder = new Forwarder();
            backendConfig.RegisterComponents(c =>
            {
                c.ConfigureComponent(_ => backendForwarder, DependencyLifecycle.SingleInstance);
            });
        }

        public async Task Start()
        {
            var frontendStartable = await Endpoint.Create(fronendConfig).ConfigureAwait(false);
            var backendStartable = await Endpoint.Create(backendConfig).ConfigureAwait(false);

            //Now we should have our dispatchers. Inialize the dispatchers.
            fronendForwarder.Initialize(backednDispatcherWrapper.CreateDispatcher());
            backendForwarder.Initialize(frontendDispatcherWrapper.CreateDispatcher());

            frontend = await frontendStartable.Start();
            backend = await backendStartable.Start();
        }

        public async Task Stop()
        {
            if (frontend != null)
            {
                await frontend.Stop();
            }
            if (backend != null)
            {
                await backend.Stop();
            }
        }
    }
}