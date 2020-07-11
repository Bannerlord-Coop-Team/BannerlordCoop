using System;
using Network.Infrastructure;

namespace Network.Protocol
{
    // TODO add class summary
    /// <summary>
    /// Defines packets
    /// </summary>
    /// <remarks>
    /// Used by PacketDispatcher
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class PacketHandlerAttribute : Attribute
    {
        public readonly EConnectionState State;
        public readonly EPacket Type;

        public PacketHandlerAttribute(EConnectionState state, EPacket eType)
        {
            State = state;
            Type = eType;
        }
    }
}
