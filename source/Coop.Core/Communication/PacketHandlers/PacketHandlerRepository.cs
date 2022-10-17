using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Communication.PacketHandlers
{
    public interface IPacketHandlerRepository
    {
        IPacketHandler<T> GetHandler<T>();
        Type GetPacketType(PacketType packetType);
    }

    public class PacketHandlerRepository : IPacketHandlerRepository
    {
        private Dictionary<Type, object> packetHandlers;
        private Dictionary<PacketType, Type> packetTypes;

        public PacketHandlerRepository()
        {
            LoadHandlers();
            LoadPacketTypeMapping();
        }

        public IPacketHandler<T> GetHandler<T>()
        {
            if (!packetHandlers.TryGetValue(typeof(T), out var handler))
                throw new KeyNotFoundException($"{typeof(T).Name} is not a packet handler.");

            return (IPacketHandler<T>)handler;
        }

        public Type GetPacketType(PacketType packetType)
        {
            if (!packetTypes.TryGetValue(packetType, out var type))
                throw new KeyNotFoundException($"{packetType} is not a package type");

            return type;
        }

        private void LoadHandlers()
        {
            var assembly = GetType().Assembly;
            var handlers = assembly.GetTypes()
                .Where(t => t.GetInterfaces()
                    .Any(i => i.GetType() == typeof(IPacketHandler<>)));

            foreach (var handler in handlers)
            {
                packetHandlers.Add(handler.GetType(), Activator.CreateInstance(handler));
            }
        }

        private void LoadPacketTypeMapping()
        {
            packetTypes = new Dictionary<PacketType, Type>
            {
                { PacketType.Example, typeof(ExamplePacket) }
            };
        }
    }
}
