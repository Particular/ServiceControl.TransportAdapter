namespace ServiceControl.TransportAdapter
{
    using System.Threading.Tasks;

    /// <summary>
    /// Transport adapter.
    /// </summary>
    public interface ITransportAdapter
    {
        /// <summary>
        /// Starts the transport adapter.
        /// </summary>
        Task Start();

        /// <summary>
        /// Stops the transport adapter.
        /// </summary>
        Task Stop();
    }
}