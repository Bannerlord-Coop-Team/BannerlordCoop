using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.PacketHandlers;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using LiteNetLib;
using TaleWorlds.CampaignSystem.Party;

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
    private readonly IObjectManager objectManager;
    private readonly IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot;

    public RequestMobilePartyBehaviorPacketHandler(
        IPacketManager packetManager,
        ISendCoalescer coalescer,
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot)
    {
        this.packetManager = packetManager;
        this.coalescer = coalescer;
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mobilePartyBehaviorSnapshot = mobilePartyBehaviorSnapshot;
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

        var data = convertedPacket.BehaviorUpdateData;

        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
    }

    private void Handle_PartyBehaviorUpdated(MessagePayload<PartyBehaviorUpdated> payload)
    {
        var data = payload.What.BehaviorUpdateData;

        // Every producer converges here. Rebuild from the authoritative server object so a client
        // request, a forced battle-finalization update, and a SetMove* capture all broadcast the
        // same complete state instead of echoing a partial or stale request payload.
        if (!objectManager.TryGetObject(data.MobilePartyId, out MobileParty party) ||
            !mobilePartyBehaviorSnapshot.TryCreateCurrent(
                party,
                out var authoritativeData))
            return;

        authoritativeData.OriginControllerId = data.OriginControllerId;
        authoritativeData.ForcePosition = data.ForcePosition;
        data = authoritativeData;

        // Coalesce per party: repeated behavior changes to one party collapse into a single latest-wins send per tick.
        var key = new CoalesceKey(PartyBehaviorUpdateChannel, Compact(data.MobilePartyId, typeof(MobileParty)));
        coalescer.Enqueue(key, new LatestWinsPayload(new NetworkUpdatePartyBehavior(data)));

        if (data.ForcePosition)
            coalescer.FlushInstance(key.InstanceId, network);
    }
}
