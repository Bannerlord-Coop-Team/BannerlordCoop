using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Client.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Messages.Behavior;
using LiteNetLib;

namespace Coop.Core.Client.Services.MobileParties.PacketHandlers
{
    /// <summary>
    /// Handles incoming <see cref="UpdatePartyBehaviorPacket"/> from server to client
    /// </summary>
    internal class UpdatePartyBehaviorPacketHandler : IPacketHandler
    {
        public PacketType PacketType => PacketType.UpdatePartyBehavior;

        private readonly IPacketManager packetManager;
        private readonly INetwork network;
        private readonly IMessageBroker messageBroker;

        public UpdatePartyBehaviorPacketHandler(
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
            UpdatePartyBehaviorPacket convertedPacket = (UpdatePartyBehaviorPacket)packet;

            messageBroker.Publish(this, new UpdatePartyBehavior(convertedPacket.BehaviorUpdateData));
        }
    }
}
