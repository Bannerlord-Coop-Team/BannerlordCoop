using System;
using System.Collections.Generic;
using System.Reflection;
using Network.Infrastructure;

namespace Network.Protocol
{
    public class PacketDispatcher
    {
        private readonly Dictionary<(EConnectionState, EPacket), List<PacketHandlerDelegate>>
            m_PacketHandlers =
                new Dictionary<(EConnectionState, EPacket), List<PacketHandlerDelegate>>();

        public event EventHandler<PacketEventArgs> OnDispatch;

        public void RegisterPacketHandlers(Type type)
        {
            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            {
                if (Attribute.IsDefined(method, typeof(PacketHandlerAttribute)))
                {
                    registerPacketHandler(method, null);
                }
            }
        }

        public void RegisterPacketHandlers(object obj)
        {
            foreach (MethodInfo method in obj
                                          .GetType()
                                          .GetMethods(
                                              BindingFlags.Public |
                                              BindingFlags.NonPublic |
                                              BindingFlags.Static |
                                              BindingFlags.Instance |
                                              BindingFlags.DeclaredOnly))
            {
                if (Attribute.IsDefined(method, typeof(PacketHandlerAttribute)))
                {
                    registerPacketHandler(method, method.IsStatic ? null : obj);
                }
            }
        }

        public void UnregisterPacketHandlers(object obj)
        {
            foreach (MethodInfo method in obj
                                          .GetType()
                                          .GetMethods(
                                              BindingFlags.Public |
                                              BindingFlags.NonPublic |
                                              BindingFlags.Static |
                                              BindingFlags.Instance |
                                              BindingFlags.DeclaredOnly))
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
            (EConnectionState state, EPacket Type) key = (state, packet.Type);
            if (m_PacketHandlers.ContainsKey(key))
            {
                foreach (PacketHandlerDelegate handler in m_PacketHandlers[key])
                {
                    handler.Invoke(packet);
                }
            }
        }

        private void registerPacketHandler(MethodInfo method, object obj)
        {
            if (!Attribute.IsDefined(method, typeof(PacketHandlerAttribute)))
            {
                throw new MissingPacketHandlerAttributeException(
                    $"Method '{method.Name}' cannot be registered as packet handler: missing PacketHandlerAttribute.");
            }

            foreach (PacketHandlerAttribute attribute in method
                .GetCustomAttributes<PacketHandlerAttribute>())
            {
                (EConnectionState State, EPacket Type) key = (attribute.State, attribute.Type);

                if (!m_PacketHandlers.ContainsKey(key))
                {
                    m_PacketHandlers[key] = new List<PacketHandlerDelegate>();
                }

                PacketHandlerDelegate del = createDelegate(method, obj);
                if (m_PacketHandlers[key].Contains(del))
                {
                    throw new DuplicatePacketHandlerRegistration(
                        $"Cannot register {del}: duplicate registration.");
                }

                m_PacketHandlers[key].Add(del);
            }
        }

        private void unregisterPacketHandler(MethodInfo method, object obj)
        {
            if (!Attribute.IsDefined(method, typeof(PacketHandlerAttribute)))
            {
                throw new MissingPacketHandlerAttributeException(
                    $"Method '{method.Name}' cannot be registered as packet handler: missing PacketHandlerAttribute.");
            }

            foreach (PacketHandlerAttribute attribute in method
                .GetCustomAttributes<PacketHandlerAttribute>())
            {
                (EConnectionState State, EPacket Type) key = (attribute.State, attribute.Type);
                if (m_PacketHandlers.ContainsKey(key))
                {
                    m_PacketHandlers[key].Remove(createDelegate(method, obj));
                }
            }
        }

        private PacketHandlerDelegate createDelegate(MethodInfo method, object obj)
        {
            if (obj == null)
            {
                return (PacketHandlerDelegate) method.CreateDelegate(typeof(PacketHandlerDelegate));
            }

            return (PacketHandlerDelegate) method.CreateDelegate(
                typeof(PacketHandlerDelegate),
                obj);
        }

        public class PacketEventArgs : EventArgs
        {
            public readonly Packet Packet;
            public readonly EConnectionState State;

            public PacketEventArgs(EConnectionState eState, Packet packet)
            {
                State = eState;
                Packet = packet;
            }
        }

        private delegate void PacketHandlerDelegate(Packet packet);
    }
}
