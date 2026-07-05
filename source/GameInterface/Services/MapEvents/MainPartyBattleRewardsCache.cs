using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

// Snapshot of MainParty's battle-reward figures, captured by MapEventRegistry.OnClientDestroyed right before it
// nulls MainParty's MapEventSide. PlayerEncounterPatches.GetBattleRewardsPrefix falls back to this when that
// teardown already removed MainParty from both MapEvent sides before the client's own scoreboard could read it
// live.
internal static class MainPartyBattleRewardsCache
{
    private static (MapEvent MapEvent, ExplainedNumber Renown, ExplainedNumber Influence, ExplainedNumber Morale, float ContributionRate)? _snapshot;

    public static void Capture(MapEvent mapEvent, MapEventParty mapEventParty, float contributionRate)
    {
        _snapshot = (mapEvent, mapEventParty.GainedRenownExplained, mapEventParty.GainedInfluenceExplained,
            mapEventParty.GainedMoraleExplained, contributionRate);
    }

    public static bool TryGet(MapEvent mapEvent, out ExplainedNumber renown, out ExplainedNumber influence,
        out ExplainedNumber morale, out float contributionRate)
    {
        if (_snapshot is { } snapshot && snapshot.MapEvent == mapEvent)
        {
            renown = snapshot.Renown;
            influence = snapshot.Influence;
            morale = snapshot.Morale;
            contributionRate = snapshot.ContributionRate;
            return true;
        }

        renown = default;
        influence = default;
        morale = default;
        contributionRate = 0f;
        return false;
    }
}
