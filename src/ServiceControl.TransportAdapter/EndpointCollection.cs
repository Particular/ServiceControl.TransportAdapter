using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Raw;

namespace ServiceControl.TransportAdapter
{
    class EndpointCollection
    {
        RawEndpointConfiguration[] endpointConfigs;
        IRawEndpointInstance[] endpoints;

        public EndpointCollection(params RawEndpointConfiguration[] endpointConfigs)
        {
            this.endpointConfigs = endpointConfigs;
        }

        public async Task Start()
        {
            endpoints = await Task.WhenAll(endpointConfigs.Select(RawEndpoint.Start).ToArray());
        }

        public Task Stop()
        {
            return Task.WhenAll(endpoints.Select(e => e.Stop()).ToArray());
        }
    }
}