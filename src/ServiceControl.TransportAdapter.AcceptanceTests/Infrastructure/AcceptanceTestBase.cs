namespace ServiceControl.TransportAdapter.AcceptanceTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Messaging;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;

    public abstract class AcceptanceTestBase
    {
        [SetUp]
        public Task ClearQueues()
        {
            return Cleanup();
        }

        Task Cleanup()
        {
            var allQueues = MessageQueue.GetPrivateQueuesByMachine("localhost");
            var queuesToBeDeleted = new List<string>();

            foreach (var messageQueue in allQueues)
            {
                using (messageQueue)
                {

                    if (messageQueue.QueueName.StartsWith(@"private$\SCTA.", StringComparison.OrdinalIgnoreCase))
                    {
                        queuesToBeDeleted.Add(messageQueue.Path);
                    }
                }
            }

            foreach (var queuePath in queuesToBeDeleted)
            {
                try
                {
                    MessageQueue.Delete(queuePath);
                    Console.WriteLine("Deleted '{0}' queue", queuePath);
                }
                catch (Exception)
                {
                    Console.WriteLine("Could not delete queue '{0}'", queuePath);
                }
            }

            MessageQueue.ClearConnectionCache();

            return Task.FromResult(0);
        }

        protected static TransportAdapterConfig<MsmqTransport, MsmqTransport> PrepareAdapterConfig()
        {
            var adapterConfig = new TransportAdapterConfig<MsmqTransport, MsmqTransport>("SCTA.ErrorForwarding.Adapter")
            {
                FronendErrorQueue = "SCTA.error-front",
                BackendErrorQueue = "SCTA.error-back",
                FrontendAuditQueue = "SCTA.audit-front",
                BackendAuditQueue = "SCTA.audit-back",
                FrontendServiceControlQueue = "SCTA.control-front",
                BackendServiceControlQueue = "SCTA.control-back"
            };
            return adapterConfig;
        }

        protected static ServiceControlFake<MsmqTransport> PrepareServiceControlFake(Action<TransportExtensions<MsmqTransport>> transportCustomization)
        {
            return new ServiceControlFake<MsmqTransport>("SCTA.audit-back", "SCTA.error-back", "SCTA.control-back", transportCustomization);
        }
    }
}