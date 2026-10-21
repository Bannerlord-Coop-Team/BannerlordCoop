using Common.Messaging;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Players;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Heroes.RomanceFlow;

public interface IPlayerDivorceRules
{
    bool CanDivorce(Hero playerHero, Hero spouse, out string reason);
    bool TryApply(Hero playerHero, Hero spouse, out bool clansSeparated, out string reason);
}

/// <summary>
/// Validates player divorce and restores independent clans through normal clan departure rules.
/// </summary>
internal class PlayerDivorceRules : IPlayerDivorceRules
{
    private readonly IPlayerManager playerManager;
    private readonly IClanLeaveRules clanLeaveRules;
    private readonly IMessageBroker messageBroker;

    public PlayerDivorceRules(IPlayerManager playerManager, IClanLeaveRules clanLeaveRules, IMessageBroker messageBroker)
    {
        this.playerManager = playerManager;
        this.clanLeaveRules = clanLeaveRules;
        this.messageBroker = messageBroker;
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

    public bool TryApply(Hero playerHero, Hero spouse, out bool clansSeparated, out string reason)
    {
        clansSeparated = false;
        if (!CanDivorce(playerHero, spouse, out reason)) return false;

        var clans = new HashSet<Clan> { playerHero.Clan, spouse.Clan };
        playerHero.Spouse = null;
        ChangeRomanticStateAction.Apply(playerHero, spouse, Romance.RomanceLevelEnum.Ended);
        if (playerHero.Spouse != null || spouse.Spouse != null ||
            Romance.GetRomanticLevel(playerHero, spouse) != Romance.RomanceLevelEnum.Ended)
        {
            reason = "The engine did not complete the divorce.";
            return false;
        }

        clansSeparated = TrySeparateClans(playerHero, spouse);
        clans.Add(playerHero.Clan);
        clans.Add(spouse.Clan);
        foreach (var clan in clans)
        {
            if (clan != null)
                messageBroker.Publish(this, new ClanManagementChanged(clan, ClanManagementRefresh.Members));
        }
        return true;
    }

    internal bool TrySeparateClans(Hero firstHero, Hero secondHero)
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
