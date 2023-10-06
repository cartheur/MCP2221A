using System;

namespace Smdn.Devices.MCP2221
{
    public class CommandException : InvalidOperationException
    {
        public CommandException(string message) : base(message) { }
        public CommandException(string message, Exception innerException) : base(message, innerException) { }
    }
}
