# Transport adapters for ServiceControl spike

In order to make it work with SC, replace the `HandleMessage` method of `ReturnToSenderDequeuer` class with this:

```
void HandleMessage(TransportMessage message)
{
    var destination = "SCAdapter.Retry";
    message.Headers.Remove("ServiceControl.Retry.StagingId");
    sender.Send(message, new SendOptions(destination));
}
```
