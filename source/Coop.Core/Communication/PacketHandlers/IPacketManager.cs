using Common.Messaging;
using Coop.Core.Messages.Network;

namespace Coop.Core.Communication.PacketHandlers
{
    public interface IPacketManager
    {
        void HandleBroadcast(MessagePayload<BroadcastPacket> payload);
        void HandleForward(MessagePayload<ForwardPacket> payload);
        void HandleSend(MessagePayload<SendPacket> payload);
        void HandleReceive(MessagePayload<ReceivePacket> payload);
    }
}