using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.PacketHandlers
{
    public enum PacketType
    {
        Invalid,
        PacketWrapper,
        Event,
        Example,
    }

    public interface IPacket
    {
        PacketType Type { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
