using NServiceBus.Features;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    class DispatcherForwarderFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var wrapper = context.Settings.Get<DispatcherWrapper>();
            context.RegisterStartupTask(b => new DispatcherInitializer(b.Build<IDispatchMessages>(), wrapper));
        }
    }
}