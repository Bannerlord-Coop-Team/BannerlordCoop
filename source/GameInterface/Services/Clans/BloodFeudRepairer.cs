using Common.Logging;
using GameInterface.Services.PlayerCaptivityService.Patches.Disable;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans;

/// <summary>
/// Clears blood feud state a loaded save carries, because the flag belongs to one player clan and coop has several (#3851).
/// </summary>
/// <remarks>
/// Temporary; once #3851 tracks feuds per player, remove this file, <see cref="Patches.BloodFeudRepairPatch"/>, its registration
/// and <c>DisableExecutionCampaignBehavior</c>, and revisit <c>KillCharacterActionPatches.ApplyExecutionTraitPenalty</c> and the
/// two text prefixes in <c>HeroExecutionScenePatch</c>, which treat every execution as a feud.
/// </remarks>
internal interface IBloodFeudRepairer
{
    /// <summary>
    /// Ends every clan's feud with the save's player clan and returns how many clans had one.
    /// </summary>
    int ClearPlayerFeuds(IEnumerable<Clan> clans);
}

/// <summary>
/// Clears the saved feud flag and its execution counters, which nothing sets while <see cref="DisableExecutionCampaignBehavior"/> is on.
/// </summary>
internal class BloodFeudRepairer : IBloodFeudRepairer
{
    private static readonly ILogger Logger = LogManager.GetLogger<BloodFeudRepairer>();

    /// <inheritdoc/>
    public int ClearPlayerFeuds(IEnumerable<Clan> clans)
    {
        int clearedCount = 0;
        foreach (Clan clan in clans)
        {
            if (clan.HasBloodFeudWithPlayer)
                clearedCount++;

            clan.HasBloodFeudWithPlayer = false;
            clan.BloodFeudExecutionsDoneCount = 0;
            clan.BloodFeudExecutionsReceivedCount = 0;
        }

        if (clearedCount > 0)
            Logger.Warning("Cleared blood feuds with the player clan for {Count} clans", clearedCount);

        return clearedCount;
    }
}
