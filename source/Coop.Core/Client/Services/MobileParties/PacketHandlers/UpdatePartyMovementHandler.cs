using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Client.Services.MobileParties.Packets;
using Coop.Core.Server.Services.MobileParties.Messages;
using LiteNetLib;

namespace Coop.Core.Client.Services.MobileParties.PacketHandlers
{
    /// <summary>
    /// Handles incoming <see cref="UpdatePartyMovementPacket"/> from server to client
    /// </summary>
    internal class UpdatePartyMovementHandler : IPacketHandler
    {
        public PacketType PacketType => PacketType.UpdateMobilePartyMovement;

        private readonly IPacketManager packetManager;
        private readonly INetwork network;
        private readonly IMessageBroker messageBroker;

        public UpdatePartyMovementHandler(
            IPacketManager packetManager,
            INetwork network,
            IMessageBroker messageBroker)
        {
            this.packetManager = packetManager;
            this.network = network;
            this.messageBroker = messageBroker;
            packetManager.RegisterPacketHandler(this);
        }

        public void Dispose()
        {
            packetManager.RemovePacketHandler(this);
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            UpdatePartyMovementPacket convertedPacket = (UpdatePartyMovementPacket)packet;

            messageBroker.Publish(this, new NetworkUpdatePartyMovement(convertedPacket.MovementData));
        }
    }
}
