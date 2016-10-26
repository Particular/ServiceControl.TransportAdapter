using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    class DispatcherInitializer : FeatureStartupTask
    {
        IDispatchMessages dispatcher;
        DispatcherWrapper wrapper;

        public DispatcherInitializer(IDispatchMessages dispatcher, DispatcherWrapper wrapper)
        {
            this.dispatcher = dispatcher;
            this.wrapper = wrapper;
        }

        protected override Task OnStart(IMessageSession session)
        {
            wrapper.SetResult(dispatcher);
            return Task.FromResult(0);
        }

        protected override Task OnStop(IMessageSession session)
        {
            return Task.FromResult(0);
        }
    }
}