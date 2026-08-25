#if DEBUG
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Missions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Commands;

internal interface IDebugBattleMissionExitRequester
{
    int Request(string mapEventId, IEnumerable<string> controllerIds);
}

internal sealed class DebugBattleMissionExitRequester : IDebugBattleMissionExitRequester
{
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IMissionMembershipRegistry missionMembership;

    public DebugBattleMissionExitRequester(
        INetwork network,
        IPlayerManager playerManager,
        IMissionMembershipRegistry missionMembership)
    {
        this.network = network;
        this.playerManager = playerManager;
        this.missionMembership = missionMembership;
    }

    public int Request(string mapEventId, IEnumerable<string> controllerIds)
    {
        int requested = 0;
        var seenControllerIds = new HashSet<string>();
        foreach (var controllerId in controllerIds)
        {
            if (!seenControllerIds.Add(controllerId) ||
                !missionMembership.IsControllerInMission(controllerId) ||
                !playerManager.TryGetPeer(controllerId, out var peer))
                continue;

            network.Send(peer, new NetworkEndDebugBattleMission(mapEventId));
            requested++;
        }

        return requested;
    }
}

/// <summary>[Server -&gt; Client] Ends a live-test fixture mission without resolving its campaign battle.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEndDebugBattleMission : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkEndDebugBattleMission(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}

/// <summary>Applies the server's live-test fixture mission-exit request on participating clients.</summary>
internal sealed class DebugBattleMissionExitHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public DebugBattleMissionExitHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkEndDebugBattleMission>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkEndDebugBattleMission>(Handle);
    }

    private void Handle(MessagePayload<NetworkEndDebugBattleMission> payload)
    {
        if (ModInformation.IsServer)
            return;

        string mapEventId = payload.What.MapEventId;
        GameThread.RunSafe(() =>
        {
            var mapEvent = MobileParty.MainParty?.MapEvent;
            if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var localMapEventId) ||
                localMapEventId != mapEventId)
                return;

            var mission = Mission.Current ?? MissionState.Current?.CurrentMission;
            mission?.EndMission();
        }, context: nameof(NetworkEndDebugBattleMission));
    }
}
#endif
