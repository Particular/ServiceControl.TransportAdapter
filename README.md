# Transport adapters for ServiceControl

Allows to use different transports for the busienss endpoints and ServiceControl. The adapter code should live along the business endpoint code. Here's how to configure an adapter:

```
var adapterConfig = new TransportAdapterConfig<SqlServerTransport, MsmqTransport>("ServiceControl.SqlServer");
adapterConfig.CustomizeFrontendTransport(InitializeSqlTransport);

adapterConfig.ConfigureIntegrationEventForwarding(
    new UnicastIntegrationEventPublishingStrategy("OtherEndpoint.IntegrationListener"),
    new UnicastIntegrationEventSubscribingStrategy());

var adapter = TransportAdapter.Create(adapterConfig);

await adapter.Start();

Console.WriteLine("Press <enter> to shutdown adapter.");
Console.ReadLine();

await adapter.Stop();
```

The code above configures the adatper between RabbitMQ transport used by the business endpoints and MSMQ transport used by ServiceControl. The messages from `audit`, `error` and `Particular.ServiceControl` in RabbitMQ are forwarded to corresponding queues in MSMQ for being consumed by ServiceControl.
Messages requested to be retried in ServicePulse are forwarded from MSMQ to a proper destination queue in RabbitMQ.
Integration event messages are routed to configured queues in the business-side transport.

### Resources

[NuGet package](https://www.nuget.org/packages/ServiceControl.TransportAdapter/)
