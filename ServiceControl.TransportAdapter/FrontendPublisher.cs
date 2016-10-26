using System.Threading.Tasks;
using NServiceBus;

namespace ServiceControl.TransportAdapter
{
    class FrontendPublisher
    {
        readonly TaskCompletionSource<IMessageSession> messageSession;

        public FrontendPublisher(TaskCompletionSource<IMessageSession> messageSession)
        {
            this.messageSession = messageSession;
        }

        public async Task Publish(object evnt)
        {
            var session = await messageSession.Task.ConfigureAwait(false);
            await session.Publish(evnt).ConfigureAwait(false);
        }
    }
}