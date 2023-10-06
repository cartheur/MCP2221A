using System;

namespace Smdn.Devices.MCP2221
{
    internal class DeviceNotSupportedException : NotSupportedException
    {
        public DeviceNotSupportedException(string message)
          : base(message)
        {
        }
    }
}

