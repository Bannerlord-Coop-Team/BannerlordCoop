using GameInterface.Services.Clans;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.RomanceFlow;

internal interface IPlayerDivorceRules
{
    bool CanDivorce(Hero playerHero, Hero spouse, out string reason);
    bool TrySeparateClans(Hero firstHero, Hero secondHero);
}

/// <summary>
/// Validates player divorce and restores independent clans through normal clan departure rules.
/// </summary>
internal class PlayerDivorceRules : IPlayerDivorceRules
{
    private readonly IPlayerManager playerManager;
    private readonly IClanLeaveRules clanLeaveRules;

    public PlayerDivorceRules(IPlayerManager playerManager, IClanLeaveRules clanLeaveRules)
    {
        this.playerManager = playerManager;
        this.clanLeaveRules = clanLeaveRules;
    }

    public bool CanDivorce(Hero playerHero, Hero spouse, out string reason)
    {
        if (playerHero == null || spouse == null || playerHero == spouse)
        {
            reason = "Two distinct heroes must exist.";
            return false;
        }

        if (!playerManager.Contains(playerHero))
        {
            reason = "The first hero must be a registered player.";
            return false;
        }

        if (!playerHero.IsAlive || !spouse.IsAlive)
        {
            reason = "Both spouses must be alive.";
            return false;
        }

        if (playerHero.Spouse != spouse || spouse.Spouse != playerHero)
        {
            reason = "The heroes are not married to each other.";
            return false;
        }

        reason = null;
        return true;
    }

    public bool TrySeparateClans(Hero firstHero, Hero secondHero)
    {
        if (firstHero == null || secondHero == null || !playerManager.Contains(firstHero) ||
            firstHero.Spouse != null || secondHero.Spouse != null)
            return false;

        if (!playerManager.Contains(secondHero) || firstHero.Clan == null || firstHero.Clan != secondHero.Clan)
            return true;

        var member = firstHero.Clan.Leader == firstHero ? secondHero : firstHero;
        return clanLeaveRules.TryApply(member, isRemoval: false);
    }
}
