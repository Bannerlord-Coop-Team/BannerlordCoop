using Common.MessageBroker;
using Coop.Mod.GameInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod
{
    internal class CoopCommunicator : ICommunicator
    {
        public IMessageBroker MessageBroker { get; }

        public IPacketManager PacketManager { get; }

        public IGameInterface GameInterface { get; }

        public CoopCommunicator(IMessageBroker messageBroker, 
                                IPacketManager packetManager, 
                                IGameInterface gameInterface)
        {
            MessageBroker = messageBroker;
            PacketManager = packetManager;
            GameInterface = gameInterface;
        }
    }
}
