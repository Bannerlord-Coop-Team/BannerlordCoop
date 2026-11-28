using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

public interface IBattleRewardLoadRepairer
{
    /// <summary>Resets NaN party morale and clan influence in a loaded save.</summary>
    /// <returns>How many parties and clans were reset.</returns>
    int RepairLoadedRewards();
}

/// <summary>
/// A NaN battle reward (#4999) left party morale and clan influence NaN in saves, which never decays and crashes the
/// party's next battle. On load both are reset to 0, the neutral value.
/// </summary>
internal sealed class BattleRewardLoadRepairer : IBattleRewardLoadRepairer
{
    private readonly ILogger logger;

    public BattleRewardLoadRepairer(ILogger logger)
    {
        this.logger = logger;
    }

    public int RepairLoadedRewards()
    {
        if (Campaign.Current?.CampaignObjectManager == null)
            return 0;

        int repaired = 0;
        foreach (MobileParty party in Campaign.Current.CampaignObjectManager.MobileParties)
        {
            if (!float.IsNaN(party._moraleDueToEvents)) continue;

            party._moraleDueToEvents = 0f;
            // The load already cached a NaN estimated strength from the NaN morale, and that cache only refreshes on roster changes
            party.Party._lastEstimatedStrengthVersionNo = -1;
            repaired++;
            logger.Warning("Reset NaN morale of party {PartyId} after load", party.StringId);
        }

        foreach (Clan clan in Clan.All)
        {
            if (!float.IsNaN(clan._influence)) continue;

            clan._influence = 0f;
            repaired++;
            logger.Warning("Reset NaN influence of clan {ClanId} after load", clan.StringId);
        }

        return repaired;
    }
}
