using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace Missions.Battles;

internal interface ILocalBattlePartyResolver
{
    string Resolve(MapEvent mapEvent);
}

internal sealed class LocalBattlePartyResolver : ILocalBattlePartyResolver
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;

    public LocalBattlePartyResolver(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
    }

    public string Resolve(MapEvent mapEvent)
    {
        string localMobilePartyId = null;
        if (playerManager.TryGetPlayer(controllerIdProvider.ControllerId, out var player))
            localMobilePartyId = player.MobilePartyId;

        return Resolve(mapEvent, PartyBase.MainParty, localMobilePartyId, objectManager);
    }

    internal static string Resolve(
        MapEvent mapEvent,
        PartyBase localParty,
        string localMobilePartyId,
        IObjectManager objectManager)
    {
        if (mapEvent?._sides == null || localParty == null || objectManager == null)
            return null;

        var localPartyId = localParty.Id;
        string logicalPartyId = null;
        foreach (var side in mapEvent._sides)
        {
            if (side == null) continue;

            foreach (var mapEventParty in side.Parties)
            {
                var party = mapEventParty?.Party;
                if (party == null ||
                    !objectManager.TryGetId(mapEventParty, out var playerMapEventPartyId))
                    continue;

                if (!string.IsNullOrEmpty(localMobilePartyId) &&
                    party.MobileParty != null &&
                    objectManager.TryGetId(party.MobileParty, out var candidateMobilePartyId) &&
                    string.Equals(candidateMobilePartyId, localMobilePartyId, StringComparison.Ordinal))
                    return playerMapEventPartyId;

                if (logicalPartyId == null && party.Id == localPartyId)
                    logicalPartyId = playerMapEventPartyId;
            }
        }

        return logicalPartyId;
    }
}
