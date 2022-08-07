using Coop.Communication.MessageBroker;
using Coop.Communication.PacketHandlers;
using Coop.Mod.GameInterfaces;

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
