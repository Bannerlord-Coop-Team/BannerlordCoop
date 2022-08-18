using Common.Messaging;
using Coop.Mod.Messages.Network;

namespace Coop.Communication.PacketHandlers
{
    public interface IPacketManager
    {
        void HandleBroadcast(MessagePayload<BroadcastPacket> payload);
        void HandleForward(MessagePayload<ForwardPacket> payload);
        void HandleSend(MessagePayload<SendPacket> payload);
        void HandleReceive(MessagePayload<ReceivePacket> payload);
    }
}