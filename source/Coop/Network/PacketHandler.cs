using System;

namespace Coop.Network
{
    // TODO add class summary
    /// <summary>
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class PacketHandlerAttribute : Attribute
    {
        public readonly EConnectionState State;
        public readonly Protocol.EPacket Type;
        public PacketHandlerAttribute(EConnectionState state, Protocol.EPacket eType)
        {
            State = state;
            Type = eType;
        }
    }
}
