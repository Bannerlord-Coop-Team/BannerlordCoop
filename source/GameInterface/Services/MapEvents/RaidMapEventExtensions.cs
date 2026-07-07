using GameInterface.Services.MobileParties.Extensions;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

public static class RaidMapEventExtensions
{
    public static bool HasMultiplePlayerParties(this MapEvent mapEvent)
    {
        if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null)
            return false;

        var playerParties = 0;
        playerParties += CountPlayerParties(mapEvent.AttackerSide);
        if (playerParties > 1)
            return true;

        playerParties += CountPlayerParties(mapEvent.DefenderSide);
        return playerParties > 1;
    }

    public static bool IsVillageHostileAction(this MapEvent mapEvent)
    {
        return mapEvent.IsRaidHostileAction() ||
               mapEvent?.Component is ForceVolunteersEventComponent ||
               mapEvent?.Component is ForceSuppliesEventComponent;
    }

    public static bool IsRaidHostileAction(this MapEvent mapEvent)
    {
        return mapEvent?.Component is RaidEventComponent;
    }

    public static bool IsVillageHostileActionWithMultiplePlayerParties(this MapEvent mapEvent)
    {
        return mapEvent.IsVillageHostileAction() && mapEvent.HasMultiplePlayerParties();
    }

    public static bool IsUnsupportedMultiPlayerHostileAction(this MapEvent mapEvent)
    {
        return mapEvent.IsVillageHostileActionWithMultiplePlayerParties() && !mapEvent.IsRaidHostileAction();
    }

    public static bool IsActiveSlowVillageRaid(this MapEvent mapEvent)
    {
        return mapEvent?.Component is RaidEventComponent &&
               !mapEvent.HasWinner &&
               (!HasDefenderTroops(mapEvent) || IsRaidAiInterventionSuppressed(mapEvent));
    }

    public static bool IsUnopposedVillageRaid(this MapEvent mapEvent)
    {
        return mapEvent?.Component is RaidEventComponent &&
               !mapEvent.HasWinner &&
               !HasDefenderTroops(mapEvent);
    }

    private static bool HasDefenderTroops(MapEvent mapEvent)
    {
        return mapEvent.DefenderSide?.TroopCount > 0;
    }

    public static bool IsRaidAiInterventionSuppressed(this MapEvent mapEvent)
    {
        return mapEvent?.Component is RaidEventComponent &&
               !mapEvent.HasWinner &&
               !MapEventConfig.AllowRaidAiIntervention &&
               mapEvent.ContainsPlayerParty() &&
               !HasDefenderPlayerParty(mapEvent);
    }

    private static bool HasDefenderPlayerParty(MapEvent mapEvent)
    {
        if (mapEvent.DefenderSide == null)
            return false;

        foreach (var mapEventParty in mapEvent.DefenderSide.Parties)
        {
            if (mapEventParty.Party?.MobileParty?.IsPlayerParty() == true)
                return true;
        }

        return false;
    }

    private static int CountPlayerParties(MapEventSide side)
    {
        var count = 0;
        foreach (var mapEventParty in side.Parties)
        {
            var mobileParty = mapEventParty.Party?.MobileParty;
            if (mobileParty != null && mobileParty.IsPlayerParty())
                count++;
        }

        return count;
    }
}