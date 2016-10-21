using System;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    class DispatcherWrapper
    {
        Func<IDispatchMessages> dispatcherFactory;
        public IDispatchMessages CreateDispatcher() => dispatcherFactory();

        public void Initialize(Func<IDispatchMessages> factory)
        {
            dispatcherFactory = factory;
        }
    }
}