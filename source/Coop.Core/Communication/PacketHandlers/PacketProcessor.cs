using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coop.Core.Communication.PacketHandlers
{
    public interface IPacketProcessor
    {
        void Process(PacketType packetType, IPacket packet);
    }

    public class PacketProcessor : IPacketProcessor
    {
        private readonly IPacketHandlerRepository packetHandlerRepository;

        public PacketProcessor(IPacketHandlerRepository packetHandlerRepository)
        {
            this.packetHandlerRepository = packetHandlerRepository;
        }

        public void Process(PacketType packetType, IPacket packet)
        {
            var type = packetHandlerRepository.GetPacketType(packetType);
            var handledType = typeof(IPacketHandler<>).MakeGenericType(type);
            var handledMethod = handledType.GetMethod(nameof(HandlePacket));

            handledMethod.Invoke(this, new object[] { packet });
        }

        private void HandlePacket<T>(IPacket packet)
        {
            var handler = packetHandlerRepository.GetHandler<T>();

            var convertedPacket = (T)packet;
            handler.HandlePacket(convertedPacket);
        }
    }
}
