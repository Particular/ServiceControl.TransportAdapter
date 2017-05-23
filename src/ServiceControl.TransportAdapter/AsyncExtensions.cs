namespace ServiceControl.TransportAdapter
{
    using System.Threading.Tasks;

    static class AsyncExtensions
    {
        public static void IgnoreContinuation(this Task task)
        {
            //Noop
        }
    }
}