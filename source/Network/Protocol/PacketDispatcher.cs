using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using Common;
using Network.Infrastructure;

namespace Network.Protocol
{
    public class PacketDispatcher
    {
        private readonly Dictionary<StatePacketPair, List<OwnerHandlerPair>>
            m_PacketHandlers =
                new Dictionary<StatePacketPair, List<OwnerHandlerPair>>();
        private readonly Dictionary<object, List<CoopStateMachine>> StateMachines = new Dictionary<object, List<CoopStateMachine>>();

        public event EventHandler<PacketEventArgs> OnDispatch;

        public void RegisterPacketHandler(Action<ConnectionBase, Packet> handler)
        {
            object owner = handler.Target;

            if (!Attribute.IsDefined(handler.Method, typeof(PacketHandlerAttribute)))
            {
                throw new MissingPacketHandlerAttributeException(
                    $"Method '{handler.Method.Name}' cannot be registered as packet handler: missing PacketHandlerAttribute.");
            }

            foreach (PacketHandlerAttribute attribute in handler.Method
                .GetCustomAttributes<PacketHandlerAttribute>())
            {
                StatePacketPair spPair = new StatePacketPair(attribute.State, attribute.Type);
                OwnerHandlerPair ohPair = new OwnerHandlerPair(owner, handler);
                if (m_PacketHandlers.ContainsKey(spPair))
                {
                    if (!m_PacketHandlers[spPair].Exists((pair) => pair == ohPair))
                    {
                        m_PacketHandlers[spPair].Add(ohPair);
                    }
                }
                else
                {
                    List<OwnerHandlerPair> list = new List<OwnerHandlerPair>();
                    list.Add(ohPair);
                    m_PacketHandlers.Add(spPair, list);
                }
            }
        }

        /// <summary>
        /// Registers <see cref="CoopStateMachine"/> to reference state
        /// </summary>
        /// <param name="owner">Assign owner</param>
        /// <param name="stateMachine">State machine to register</param>
        public void RegisterStateMachine(object owner, CoopStateMachine stateMachine)
        {
            if (StateMachines.ContainsKey(owner) && !StateMachines[owner].Contains(stateMachine))
            {
                StateMachines[owner].Add(stateMachine);
            }
            else
            {
                StateMachines.Add(owner, new List<CoopStateMachine>() { stateMachine });
            }
        }

        public void UnregisterPacketHandler(Action<ConnectionBase, Packet> handler)
        {
            foreach(List<OwnerHandlerPair> handlerList in m_PacketHandlers.Values)
            {
                handlerList.Remove(new OwnerHandlerPair(handler.Target, handler));
            }
        }

        public void UnregisterPacketHandlers(object owner)
        {
            StateMachines.Remove(owner);
            foreach (List<OwnerHandlerPair> handlerList in m_PacketHandlers.Values)
            {
                handlerList.RemoveAll((pair) => pair.Owner == owner);
            }
        }

        public void Dispatch(ConnectionBase connection, Packet packet)
        {
            OnDispatch?.Invoke(this, new PacketEventArgs(packet));
            Dictionary<object, List<CoopStateMachine>> stateMachinesCopy = new Dictionary<object, List<CoopStateMachine>>(StateMachines);
            foreach (List<CoopStateMachine> stateMachines in stateMachinesCopy.Values)
            {
                foreach (CoopStateMachine stateMachine in stateMachines)
                {
                    StatePacketPair key = new StatePacketPair(stateMachine.State, packet.Type);
                    if (m_PacketHandlers.ContainsKey(key))
                    {
                        foreach (OwnerHandlerPair pair in m_PacketHandlers[key])
                        {
                            pair.Handler.Invoke(connection, packet);
                        }
                    }
                }
            }
        }

        public class PacketEventArgs : EventArgs
        {
            public readonly Packet Packet;

            public PacketEventArgs(Packet packet)
            {
                Packet = packet;
            }
        }

        private delegate void PacketHandlerDelegate(Packet packet);
    }

    #region Packaging Classes
    internal class StatePacketPair
    {
        public readonly Enum State;
        public readonly EPacket ePacket;
        public StatePacketPair(Enum state, EPacket ePacket)
        {
            State = state;
            this.ePacket = ePacket;
        }

        public static bool operator ==(StatePacketPair lhs, StatePacketPair rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(StatePacketPair lhs, StatePacketPair rhs)
        {
            return !(lhs.Equals(rhs));
        }

        public override bool Equals(object obj)
        {
            return obj is StatePacketPair pair &&
                   EqualityComparer<Enum>.Default.Equals(State, pair.State) &&
                   ePacket == pair.ePacket;
        }

        public override int GetHashCode()
        {
            int hashCode = EqualityComparer<Enum>.Default.GetHashCode(State);
            hashCode += ePacket.GetHashCode();
            return hashCode;
        }
    }

    internal class OwnerHandlerPair
    {
        public readonly object Owner;
        public readonly Action<ConnectionBase, Packet> Handler;
        public OwnerHandlerPair(object owner, Action<ConnectionBase, Packet> handler)
        {
            Owner = owner;
            Handler = handler;
        }

        public override bool Equals(object obj)
        {
            return obj is OwnerHandlerPair pair &&
                   EqualityComparer<object>.Default.Equals(Owner, pair.Owner) &&
                   EqualityComparer<Action<ConnectionBase, Packet>>.Default.Equals(Handler, pair.Handler);
        }

        public override int GetHashCode()
        {
            int hashCode = EqualityComparer<object>.Default.GetHashCode(Owner);
            hashCode += EqualityComparer<Action<ConnectionBase, Packet>>.Default.GetHashCode(Handler);
            return hashCode;
        }

        public static bool operator ==(OwnerHandlerPair lhs, OwnerHandlerPair rhs)
        {
            return lhs.Owner == rhs.Owner && lhs.Handler == rhs.Handler;
        }

        public static bool operator !=(OwnerHandlerPair lhs, OwnerHandlerPair rhs)
        {
            return !(lhs == rhs);
        }
    }
    #endregion
}
