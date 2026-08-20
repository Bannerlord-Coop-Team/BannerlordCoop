using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Participation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Missions.Messages;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>Tracks deliberate battle withdrawals separately from structural map-event membership.</summary>
internal sealed class ServerRetreatedMapEventPartyHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IRetreatedMapEventPartyTracker tracker;

    public ServerRetreatedMapEventPartyHandler(IMessageBroker messageBroker, IObjectManager objectManager, IPlayerManager playerManager, IRetreatedMapEventPartyTracker tracker)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.tracker = tracker;

        messageBroker.Subscribe<NetworkBattleRetreated>(Handle_NetworkBattleRetreated);
        messageBroker.Subscribe<BattlePartyRetreated>(Handle_BattlePartyRetreated);
        messageBroker.Subscribe<MissionMemberEntered>(Handle_MissionMemberEntered);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleRetreated>(Handle_NetworkBattleRetreated);
        messageBroker.Unsubscribe<BattlePartyRetreated>(Handle_BattlePartyRetreated);
        messageBroker.Unsubscribe<MissionMemberEntered>(Handle_MissionMemberEntered);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    private void Handle_NetworkBattleRetreated(MessagePayload<NetworkBattleRetreated> payload)
    {
        if (payload.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player))
        {
            return;
        }

        messageBroker.Publish(this, new BattlePartyRetreated(player.ControllerId, payload.What.InstanceId));
    }

    private void Handle_BattlePartyRetreated(MessagePayload<BattlePartyRetreated> payload)
    {
        if (TryResolve(payload.What.ControllerId, payload.What.InstanceId, out var mapEvent, out var party))
            tracker.MarkRetreated(mapEvent, party);
    }

    private void Handle_MissionMemberEntered(MessagePayload<MissionMemberEntered> payload)
    {
        if (TryResolve(payload.What.ControllerId, payload.What.InstanceId, out var mapEvent, out var party))
            tracker.MarkReentered(mapEvent, party);
    }

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        tracker.Clear(payload.What.MapEvent);
    }

    private bool TryResolve(string controllerId, string mapEventId, out MapEvent mapEvent, out PartyBase party)
    {
        mapEvent = null;
        party = null;

        if (!playerManager.TryGetPlayer(controllerId, out var player) ||
            !objectManager.TryGetObject(player.MobilePartyId, out MobileParty mobileParty) ||
            !objectManager.TryGetObject(mapEventId, out mapEvent))
        {
            return false;
        }

        if (mobileParty.MapEvent != null && !ReferenceEquals(mobileParty.MapEvent, mapEvent)) return false;

        party = mobileParty.Party;
        return party != null;
    }
}