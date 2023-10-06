using System;

namespace Smdn.Devices.MCP2221
{
    public class DeviceNotFoundException : InvalidOperationException
    {
        public DeviceNotFoundException()
          : base("MCP2221/MCP2221A not found")
        {
        }

        public DeviceNotFoundException(string message)
          : base(message)
        {
        }
    }
}
