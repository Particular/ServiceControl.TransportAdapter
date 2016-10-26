using System;
using System.Linq;
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
        TaskCompletionSource<IMessageSession> frontendSession = new TaskCompletionSource<IMessageSession>();
        TaskCompletionSource<IDispatchMessages> frontendDispatcher = new TaskCompletionSource<IDispatchMessages>();
        TaskCompletionSource<IDispatchMessages> backendDispatcher = new TaskCompletionSource<IDispatchMessages>();
        ConventionsBuilder backendConventions;

        public ServiceControlAdapter(string name, Action<EndpointConfiguration, TransportExtensions<TFrontendTransport>> provideFrontendConfiguration)
        {
            fronendConfig = new EndpointConfiguration(name);
            var transport = fronendConfig.UseTransport<TFrontendTransport>();
            fronendConfig.UsePersistence<InMemoryPersistence>();
            fronendConfig.SendFailedMessagesTo("error"); //Not used
            fronendConfig.EnableFeature<ServiceControlFrontendAdapterFeature>();
            frontendDispatcherWrapper = new DispatcherWrapper();
            fronendConfig.GetSettings().Set<DispatcherWrapper>(frontendDispatcherWrapper);
            fronendConfig.Conventions().DefiningEventsAs(IsEvent);
            fronendConfig.DisableFeature<TimeoutManager>();
            fronendConfig.ExcludeTypes(typeof(IntegrationEventForwarder));
            fronendForwarder = new Forwarder();
            fronendConfig.RegisterComponents(c =>
            {
                c.ConfigureComponent(_ => fronendForwarder, DependencyLifecycle.SingleInstance);
            });
            provideFrontendConfiguration(fronendConfig, transport);

            var frontEndPublisher = new FrontendPublisher(frontendSession);

            backendConfig = new EndpointConfiguration(name);
            var backendRouting = backendConfig.UseTransport<MsmqTransport>().Routing(); //Always use MSMQ on the backend
            backendRouting.RegisterPublisher(typeof(Contracts.CustomCheckFailed).Assembly, "Particular.ServiceControl");
            backendConventions = backendConfig.Conventions();
            backendConventions.DefiningEventsAs(IsEvent);
            backendConfig.UsePersistence<InMemoryPersistence>();
            backendConfig.UseSerialization<JsonSerializer>();
            backendConfig.DisableFeature<TimeoutManager>();
            backendConfig.SendFailedMessagesTo("error"); //Not used because the main queue is not used.
            backendConfig.EnableFeature<ServiceControlBackendAdaperFeature>();
            backednDispatcherWrapper = new DispatcherWrapper();
            backendConfig.GetSettings().Set<DispatcherWrapper>(backednDispatcherWrapper);
            backendForwarder = new Forwarder();
            backendConfig.RegisterComponents(c =>
            {
                c.ConfigureComponent(_ => frontEndPublisher, DependencyLifecycle.SingleInstance);
                c.ConfigureComponent(_ => backendForwarder, DependencyLifecycle.SingleInstance);
            });
        }

        static bool IsEvent(Type t)
        {
            return t.Namespace == "ServiceControl.Contracts" ||
                (typeof(IEvent).IsAssignableFrom(t) && typeof(IEvent) != t);
        }

        public async Task Start()
        {
            var frontendStartable = await Endpoint.Create(fronendConfig).ConfigureAwait(false);
            var backendStartable = await Endpoint.Create(backendConfig).ConfigureAwait(false);

            //Now we should have our dispatchers. Inialize the dispatchers.
            fronendForwarder.Initialize(backednDispatcherWrapper.CreateDispatcher());
            backendForwarder.Initialize(frontendDispatcherWrapper.CreateDispatcher());

            frontend = await frontendStartable.Start();
            frontendSession.SetResult(frontend);
            backend = await backendStartable.Start();

            foreach (var type in typeof(Contracts.CustomCheckFailed).Assembly.GetTypes().Where(t => backendConventions.Conventions.IsEventType(t)))
            {
                await backend.Subscribe(type);
            }
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