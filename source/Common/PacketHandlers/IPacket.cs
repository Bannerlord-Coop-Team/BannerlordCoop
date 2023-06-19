﻿using LiteNetLib;

namespace Common.PacketHandlers
{
    public enum PacketType
    {
        Invalid,
        PacketWrapper,
        Event,
        Example,
        Test,
        Movement,
        RequestMobilePartyBehavior,
        UpdatePartyBehavior,

    }

    public interface IPacket
    {
        PacketType PacketType { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
