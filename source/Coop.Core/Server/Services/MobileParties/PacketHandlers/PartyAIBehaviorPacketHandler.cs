using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.PacketHandlers;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using LiteNetLib;

namespace Coop.Core.Server.Services.MobileParties.PacketHandlers;

/// <summary>
/// Handles incoming <see cref="RequestMobilePartyBehaviorPacket"/>
/// </summary>
internal class RequestMobilePartyBehaviorPacketHandler : IPacketHandler
{
    // Coalescer channel for per-party behavior updates; only the latest behavior per party is sent each tick.
    private const string PartyBehaviorUpdateChannel = "PartyBehaviorUpdate";

    public PacketType PacketType => PacketType.RequestUpdatePartyBehavior;

    private readonly IPacketManager packetManager;
    private readonly ISendCoalescer coalescer;
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IPartyBehaviorWireMapper wireMapper;

    public RequestMobilePartyBehaviorPacketHandler(
        IPacketManager packetManager,
        ISendCoalescer coalescer,
        IMessageBroker messageBroker,
        INetwork network,
        IPartyBehaviorWireMapper wireMapper)
    {
        this.packetManager = packetManager;
        this.coalescer = coalescer;
        this.messageBroker = messageBroker;
        this.network = network;
        this.wireMapper = wireMapper;
        packetManager.RegisterPacketHandler(this);

        messageBroker.Subscribe<PartyBehaviorUpdated>(Handle_PartyBehaviorUpdated);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);

        messageBroker.Unsubscribe<PartyBehaviorUpdated>(Handle_PartyBehaviorUpdated);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        RequestMobilePartyBehaviorPacket convertedPacket = (RequestMobilePartyBehaviorPacket)packet;

        if (!wireMapper.TryFromNetwork(convertedPacket.BehaviorUpdateData, out var data)) return;

        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
    }

    private void Handle_PartyBehaviorUpdated(MessagePayload<PartyBehaviorUpdated> payload)
    {
        var data = payload.What.BehaviorUpdateData;

        // Coalesce per party: repeated behavior changes to one party collapse into a single latest-wins send per tick.
        if (!wireMapper.TryToNetwork(data, out var networkData)) return;
        var key = new CoalesceKey(PartyBehaviorUpdateChannel, networkData.MobilePartyId);
        coalescer.Enqueue(key, new LatestWinsPayload(new NetworkUpdatePartyBehavior(networkData)));

        if (data.ForcePosition)
            coalescer.FlushInstance(key.InstanceId, network);
    }
}
