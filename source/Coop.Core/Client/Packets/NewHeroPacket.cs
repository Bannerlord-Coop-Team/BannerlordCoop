using Common.Serialization;
using Coop.Core.Communication.PacketHandlers;
using GameInterface.Serialization.External;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Core.Client.Packets
{
    internal class NewHeroPacket : IPacket
    {
        public PacketType PacketType => PacketType.Hero;

        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

        public HeroBinaryPackage HeroBinaryPackage { 
            get { return Deserialize(_data); } 
            set { _data = Serialize(value); }
        }
        private byte[] _data;

        public NewHeroPacket(HeroBinaryPackage heroBinaryPackage)
        {
            HeroBinaryPackage = heroBinaryPackage;
        }

        private HeroBinaryPackage Deserialize(byte[] data)
        {
            return BinaryFormatterSerializer.Deserialize<HeroBinaryPackage>(data);
        }

        private byte[] Serialize(HeroBinaryPackage package)
        {
            return BinaryFormatterSerializer.Serialize(package);
        }
    }
}
