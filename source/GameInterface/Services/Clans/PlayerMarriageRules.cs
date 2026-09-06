using GameInterface.Services.Clans.Data;
using GameInterface.Services.Heroes.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans;

public interface IPlayerMarriageRules : IGameAbstraction
{
    bool CanMarry(Hero firstHero, Hero secondHero);
    ClanJoinUnavailableReason GetClanJoinUnavailableReason(Hero firstHero, Hero secondHero, bool matrilineal);
    bool TryApply(Hero firstHero, Hero secondHero, bool matrilineal);
}

public class PlayerMarriageRules : IPlayerMarriageRules
{
    private readonly IClanJoinRules clanJoinRules;

    public PlayerMarriageRules(IClanJoinRules clanJoinRules)
    {
        this.clanJoinRules = clanJoinRules;
    }

    public bool CanMarry(Hero firstHero, Hero secondHero)
        => firstHero != null && secondHero != null && firstHero != secondHero &&
           firstHero.IsPlayerHero() && secondHero.IsPlayerHero() &&
           Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(firstHero, secondHero);

    public ClanJoinUnavailableReason GetClanJoinUnavailableReason(Hero firstHero, Hero secondHero, bool matrilineal)
    {
        var leader = firstHero.IsFemale == matrilineal ? firstHero : secondHero;
        var member = leader == firstHero ? secondHero : firstHero;
        if (leader.Clan == null || member.Clan == null) return ClanJoinUnavailableReason.MissingClan;
        if (leader.Clan.Leader != leader) return ClanJoinUnavailableReason.TargetIsNotClanLeader;
        if (member.Clan == leader.Clan) return ClanJoinUnavailableReason.None;

        return clanJoinRules.GetUnavailableReason(member, leader);
    }

    public bool TryApply(Hero firstHero, Hero secondHero, bool matrilineal)
    {
        if (!CanMarry(firstHero, secondHero) ||
            GetClanJoinUnavailableReason(firstHero, secondHero, matrilineal) != ClanJoinUnavailableReason.None)
            return false;

        var leader = firstHero.IsFemale == matrilineal ? firstHero : secondHero;
        var member = leader == firstHero ? secondHero : firstHero;
        if (member.Clan != leader.Clan)
            clanJoinRules.Apply(member, leader.Clan);

        // Joining first avoids vanilla removing the spouse from their player party.
        MarriageAction.Apply(leader, member);
        return firstHero.Spouse == secondHero && secondHero.Spouse == firstHero;
    }
}
