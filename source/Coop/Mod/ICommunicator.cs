using Coop.Mod.GameInterfaces;
using Coop.Communication.MessageBroker;
using Coop.Communication.PacketHandlers;

namespace Coop.Mod
{
    public interface ICommunicator
    {
        IMessageBroker MessageBroker { get; }
        IPacketManager PacketManager { get; }
        IGameInterface GameInterface { get; }
    }
}
