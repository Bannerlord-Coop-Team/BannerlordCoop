using System;
using Network.Infrastructure;

namespace Network.Protocol
{
    // TODO add class summary
    /// <summary>
    /// Defines packet handlers
    /// 
    /// </summary>
    /// <remarks>
    /// Used by PacketDispatcher
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class PacketHandlerAttribute : Attribute
    {
        public Enum State { get; protected set; }
        public EPacket Type { get; protected set; }
    }
}
