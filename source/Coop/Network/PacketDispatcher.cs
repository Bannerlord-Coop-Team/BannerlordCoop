using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Network
{
    public class PacketDispatcher
    {
        public class PacketEventArgs : EventArgs
        {
            public PacketEventArgs(EConnectionState eState, Packet packet)
            {
                State = eState;
                Packet = packet;
            }
            public readonly EConnectionState State;
            public readonly Packet Packet;
        }
        public event EventHandler<PacketEventArgs> OnDispatch;
        private delegate void PacketHandlerDelegate(Packet packet);
        private readonly Dictionary<(EConnectionState, Protocol.EPacket), List<PacketHandlerDelegate>> m_PacketHandlers = new Dictionary<(EConnectionState, Protocol.EPacket), List<PacketHandlerDelegate>>();
        public PacketDispatcher()
        {
        }
        public void RegisterPacketHandlers(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (Attribute.IsDefined(method, typeof(PacketHandlerAttribute)))
                {
                    registerPacketHandler(method, null);
                }
            }
        }
        public void RegisterPacketHandlers(object obj)
        {
            foreach (var method in obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (Attribute.IsDefined(method, typeof(PacketHandlerAttribute)))
                {
                    registerPacketHandler(method, method.IsStatic ? null : obj);                    
                }
            }
        }
        public void UnregisterPacketHandlers(object obj)
        {
            foreach (var method in obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (Attribute.IsDefined(method, typeof(PacketHandlerAttribute)))
                {
                    unregisterPacketHandler(method, obj);
                }
            }
            
        }
        public void Dispatch(EConnectionState state, Packet packet)
        {
            OnDispatch?.Invoke(this, new PacketEventArgs(state, packet));
            var key = (state, packet.Type);
            if (m_PacketHandlers.ContainsKey(key))
            {
                foreach (var handler in m_PacketHandlers[key])
                {
                    handler.Invoke(packet);
                }
            }
        }
        private void registerPacketHandler(MethodInfo method, object obj)
        {
            if (!Attribute.IsDefined(method, typeof(PacketHandlerAttribute)))
            {
                throw new MissingPacketHandlerAttributeException($"Method '{method.Name}' cannot be registered as packet handler: missing PacketHandlerAttribute.");
            }

            foreach (var attribute in method.GetCustomAttributes<PacketHandlerAttribute>())
            {
                var key = (attribute.State, attribute.Type);

                if(!m_PacketHandlers.ContainsKey(key))
                {
                    m_PacketHandlers[key] = new List<PacketHandlerDelegate>();
                }

                PacketHandlerDelegate del = createDelegate(method, obj);
                if(m_PacketHandlers[key].Contains(del))
                {
                    throw new DuplicatePacketHandlerRegistration($"Cannot register {del}: duplicate registration.");
                }
                m_PacketHandlers[key].Add(del);
                
            }
        }
        private void unregisterPacketHandler(MethodInfo method, object obj)
        {
            if (!Attribute.IsDefined(method, typeof(PacketHandlerAttribute)))
            {
                throw new MissingPacketHandlerAttributeException($"Method '{method.Name}' cannot be registered as packet handler: missing PacketHandlerAttribute.");
            }

            foreach (var attribute in method.GetCustomAttributes<PacketHandlerAttribute>())
            {
                var key = (attribute.State, attribute.Type);
                if(m_PacketHandlers.ContainsKey(key))
                {
                    m_PacketHandlers[key].Remove(createDelegate(method, obj));
                }
            }
        }

        private PacketHandlerDelegate createDelegate(MethodInfo method, object obj)
        {
            if (obj == null)
            {
                return (PacketHandlerDelegate)method.CreateDelegate(typeof(PacketHandlerDelegate));
            }
            else
            {
                return (PacketHandlerDelegate)method.CreateDelegate(typeof(PacketHandlerDelegate), obj);
            }
        }
    }
}
