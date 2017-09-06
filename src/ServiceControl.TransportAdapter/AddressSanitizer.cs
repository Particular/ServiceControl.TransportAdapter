namespace ServiceControl.TransportAdapter
{
    using System;
    using System.Linq;

    class AddressSanitizer
    {
        public static string MakeV5CompatibleAddress(string address)
        {
            var parts = address.Split(new[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
            var v5compatibleAddress = string.Join("@", parts.Take(2));
            return v5compatibleAddress;
        }
    }
}