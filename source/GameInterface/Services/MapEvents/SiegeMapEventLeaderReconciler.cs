using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents;

internal interface ISiegeMapEventLeaderReconciler
{
    bool RestoreAfterJoin(MapEvent mapEvent, PartyBase joinedParty);

    bool RestoreBeforeFinalize(
        MapEvent mapEvent,
        out PartyBase replacedLeader,
        out PartyBase restoredLeader);
}

internal class SiegeMapEventLeaderReconciler : ISiegeMapEventLeaderReconciler
{
    public bool RestoreAfterJoin(MapEvent mapEvent, PartyBase joinedParty)
    {
        if (!TryGetBesiegerLeadership(mapEvent, out var side, out var campLeader) ||
            campLeader != joinedParty)
        {
            return false;
        }

        return Restore(side, campLeader, out _);
    }

    public bool RestoreBeforeFinalize(
        MapEvent mapEvent,
        out PartyBase replacedLeader,
        out PartyBase restoredLeader)
    {
        replacedLeader = null;
        restoredLeader = null;
        if (!TryGetBesiegerLeadership(mapEvent, out var side, out var campLeader))
            return false;

        restoredLeader = campLeader;
        return Restore(side, campLeader, out replacedLeader);
    }

    private static bool TryGetBesiegerLeadership(
        MapEvent mapEvent,
        out MapEventSide side,
        out PartyBase campLeader)
    {
        side = null;
        campLeader = null;
        if (mapEvent == null)
            return false;

        if (mapEvent.IsSallyOut || mapEvent.IsBlockadeSallyOut)
            side = mapEvent.DefenderSide;
        else if (mapEvent.IsSiegeAssault)
            side = mapEvent.AttackerSide;
        else
            return false;

        campLeader = mapEvent.MapEventSettlement?.SiegeEvent?.BesiegerCamp?.LeaderParty?.Party;
        return side != null && campLeader != null && campLeader.MapEventSide == side;
    }

    private static bool Restore(
        MapEventSide side,
        PartyBase campLeader,
        out PartyBase replacedLeader)
    {
        replacedLeader = side.LeaderParty;
        if (replacedLeader == campLeader)
            return false;

        side.LeaderParty = campLeader;
        side._mapFaction = campLeader.MapFaction;
        side.CacheLeaderSimulationModifier();
        return true;
    }
}
