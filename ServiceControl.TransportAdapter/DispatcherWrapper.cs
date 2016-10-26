using System;
using System.Threading.Tasks;
using NServiceBus.Transport;

namespace ServiceControl.TransportAdapter
{
    class DispatcherWrapper
    {
        TaskCompletionSource<IDispatchMessages> dispatcherFuture;

        public DispatcherWrapper(TaskCompletionSource<IDispatchMessages> dispatcherFuture)
        {
            this.dispatcherFuture = dispatcherFuture;
        }

        public void SetResult(IDispatchMessages result)
        {
            dispatcherFuture.SetResult(result);
        }
    }
}