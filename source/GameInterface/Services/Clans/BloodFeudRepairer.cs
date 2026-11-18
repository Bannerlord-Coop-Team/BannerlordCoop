using Common.Logging;
using GameInterface.Services.BloodFeuds.Interfaces;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans;

/// <summary>
/// Clears blood feud state a loaded save carries, because the flag belongs to one player clan and coop has several.
/// </summary>
/// <remarks>
/// <see cref="IExecutionCampaignBehaviorInterface"/> keeps each player's feuds in the co-op session and each client applies its
/// own player's after the load, so the save's own feuds would only be another player clan's.
/// </remarks>
internal interface IBloodFeudRepairer
{
    /// <summary>
    /// Ends every clan's feud with the save's player clan and returns how many clans had one.
    /// </summary>
    int ClearPlayerFeuds(IEnumerable<Clan> clans);
}

/// <summary>
/// Clears the saved feud flag and its execution counters, which the server never sets.
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
