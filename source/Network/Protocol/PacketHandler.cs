using System;
using Network.Infrastructure;

namespace Network.Protocol
{
    /// <summary>
    ///     Attribute to mark methods that handle incoming packets. 
    /// </summary>
    /// <remarks>
    ///     Used by PacketDispatcher
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class PacketHandlerAttribute : Attribute
    {
        public Enum State { get; protected set; }
        public EPacket Type { get; protected set; }
    }
}
