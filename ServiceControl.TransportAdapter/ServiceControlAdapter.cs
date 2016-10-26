using System;
using System.Linq;
using System.Threading.Tasks;
using Metrics;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    public class ServiceControlAdapter<TFrontendTransport>
        where TFrontendTransport : TransportDefinition, new()
    {
        EndpointConfiguration frontendConfig;
        EndpointConfiguration backendConfig;
        IEndpointInstance frontend;
        IEndpointInstance backend;
        TaskCompletionSource<IMessageSession> frontendSession = new TaskCompletionSource<IMessageSession>();
        ConventionsBuilder backendConventions;

        public ServiceControlAdapter(string name, Action<EndpointConfiguration, TransportExtensions<TFrontendTransport>> provideFrontendConfiguration)
        {
            frontendConfig = new EndpointConfiguration(name);
            var transport = frontendConfig.UseTransport<TFrontendTransport>().Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            frontendConfig.UsePersistence<InMemoryPersistence>();
            frontendConfig.SendFailedMessagesTo("error"); //Not used
            frontendConfig.EnableFeature<ServiceControlFrontendAdapterFeature>();
            frontendConfig.Conventions().DefiningEventsAs(IsEvent);
            frontendConfig.DisableFeature<TimeoutManager>();
            frontendConfig.ExcludeTypes(typeof(IntegrationEventForwarder));

            provideFrontendConfiguration(frontendConfig, transport);
            var frontEndPublisher = new FrontendPublisher(frontendSession);

            backendConfig = new EndpointConfiguration(name);
            var backendTransport = backendConfig.UseTransport<MsmqTransport>();
            backendTransport.Transactions(TransportTransactionMode.TransactionScope);
            backendTransport.Routing().RegisterPublisher(typeof(Contracts.CustomCheckFailed).Assembly, "Particular.ServiceControl");
            backendConventions = backendConfig.Conventions();
            backendConventions.DefiningEventsAs(IsEvent);
            backendConfig.UsePersistence<InMemoryPersistence>();
            backendConfig.UseSerialization<JsonSerializer>();
            backendConfig.DisableFeature<TimeoutManager>();
            backendConfig.SendFailedMessagesTo("error"); //Not used because the main queue is not used.
            backendConfig.EnableFeature<ServiceControlBackendAdaperFeature>();
            backendConfig.RegisterComponents(c =>
            {
                c.ConfigureComponent(_ => frontEndPublisher, DependencyLifecycle.SingleInstance);
            });

            Forwarding.EnableTwoWayDispatcherForwarding(frontendConfig, backendConfig);
        }

        static bool IsEvent(Type t)
        {
            return t.Namespace == "ServiceControl.Contracts" ||
                (typeof(IEvent).IsAssignableFrom(t) && typeof(IEvent) != t);
        }

        public async Task Start()
        {
            var frontendStartable = await Endpoint.Create(frontendConfig).ConfigureAwait(false);
            var backendStartable = await Endpoint.Create(backendConfig).ConfigureAwait(false);

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