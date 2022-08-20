using LiteNetLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Core.Communication.PacketHandlers
{
    public class ExamplePacketHandler : IPacketHandler
    {

        /// <summary>
        /// Handler type must match packet type
        /// </summary>
        public PacketType PacketType => PacketType.Example;

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            ExamplePacket convertedPacket = (ExamplePacket)packet;

            if (convertedPacket.Data != null)
            {
                // Do something with data
            }
        }
    }

    [ProtoContract]
    public readonly struct ExamplePacket : IPacket
    {
        [ProtoMember(1)]
        public PacketType Type => PacketType.Example;
        [ProtoMember(2)]
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableSequenced;
        [ProtoMember(3)]
        public byte[] Data { get; }

        public ExamplePacket(byte[] data)
        {
            Data = data;
        }
    }
}
