using GameInterface.Services.Heroes.Extensions;
using Helpers;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans;

public interface ILeaderlessClanResolver
{
    /// <summary>
    /// Gives a kingdom clan without a usable leader a new one, or closes the clan when nobody in it can lead.
    /// </summary>
    void Resolve(Clan clan);

    /// <summary>Resolves every leaderless kingdom clan in a loaded save.</summary>
    /// <returns>How many clans were resolved.</returns>
    int RepairLoadedClans();

    /// <summary>An adult in the clan who can lead it, other than <paramref name="excluded"/>, or null.</summary>
    Hero FindSuccessor(Clan clan, Hero excluded = null);

    /// <summary>
    /// Destroys a clan nobody can lead, after moving its children to a relative's or another lord clan and taking its
    /// disabled (retired) heroes out, since the game's clan destruction kills every living member.
    /// </summary>
    void CloseClan(Clan clan);
}

/// <summary>
/// A kingdom clan whose leader is missing or disabled breaks every kingdom screen calculation that asks
/// each clan leader for relations (#4166: the settlement claim likelihood threw every frame and the
/// kingdom screen stayed black for the whole kingdom). This happened after a player retired with no heir.
/// The clan gets an adult member as leader, or is closed like a player clan that ends without heirs.
/// </summary>
internal sealed class LeaderlessClanResolver : ILeaderlessClanResolver
{
    private readonly ILogger logger;

    public LeaderlessClanResolver(ILogger logger)
    {
        this.logger = logger;
    }

    public void Resolve(Clan clan)
    {
        if (!NeedsLeader(clan)) return;

        var successor = FindSuccessor(clan);
        if (successor == null)
        {
            logger.Warning("Kingdom clan {ClanId} had no leader and nobody who can lead it, closing it", clan.StringId);
            CloseClan(clan);
            return;
        }

        logger.Warning("Kingdom clan {ClanId} had no leader, appointing {HeroId}", clan.StringId, successor.StringId);
        if (clan.Leader != null)
            ChangeClanLeaderAction.ApplyWithSelectedNewLeader(clan, successor);
        else
            AppointWithoutOldLeader(clan, successor);
    }

    public int RepairLoadedClans()
    {
        var leaderless = Clan.All.Where(NeedsLeader).ToList();
        foreach (var clan in leaderless)
        {
            try
            {
                Resolve(clan);
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to resolve leaderless kingdom clan {ClanId}", clan.StringId);
            }
        }

        return leaderless.Count;
    }

    // Same bar as an heir, but not limited by where the hero is (a prisoner can lead a clan).
    // AI family is preferred over other players, matching the retirement successor order.
    public Hero FindSuccessor(Clan clan, Hero excluded = null) =>
        clan.Heroes
            .Where(hero =>
                hero != excluded &&
                hero.IsAlive &&
                !hero.IsDisabled &&
                !hero.IsNotSpawned &&
                !hero.IsChild &&
                !hero.IsWanderer &&
                !hero.IsNotable &&
                hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge &&
                hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
            .OrderBy(hero => hero.IsPlayerHero())
            .FirstOrDefault();

    public void CloseClan(Clan clan)
    {
        foreach (var hero in clan.Heroes.Where(hero => hero.IsAlive && !hero.IsWanderer).ToList())
        {
            if (hero.IsDisabled)
            {
                // a retired hero keeps its state and retirement record
                hero.Clan = null;
                continue;
            }

            var home = FindHomeClan(hero, clan);
            if (home == null)
            {
                // no other lord clan is left, so the hero is kept alive outside the clan like a retired one
                logger.Warning("No clan can take {HeroId}, leaving them without a clan before closing clan {ClanId}", hero.StringId, clan.StringId);
                hero.Clan = null;
                continue;
            }

            logger.Information("Moving {HeroId} to clan {HomeClanId} before closing clan {ClanId}", hero.StringId, home.StringId, clan.StringId);
            hero.Clan = home;
        }

        // DestroyClanAction also kills the clan leader directly, so a retired leader must not stay in the slot
        if (clan.Leader != null && clan.Leader.IsDisabled)
            clan.SetLeader(null);

        DestroyClanAction.Apply(clan);
    }

    internal static bool NeedsLeader(Clan clan) =>
        clan != null &&
        !clan.IsEliminated &&
        !clan.IsBanditFaction &&
        clan.Kingdom != null &&
        (clan.Leader == null || clan.Leader.IsDisabled);

    // ChangeClanLeaderAction hands the old leader's gold over and throws without one, so the rest of
    // what it does is repeated here: governor, party, the leader itself and the leader-changed event.
    private static void AppointWithoutOldLeader(Clan clan, Hero successor)
    {
        if (successor.GovernorOf != null)
            ChangeGovernorAction.RemoveGovernorOf(successor);

        if (!successor.IsPrisoner && !successor.IsFugitive && !successor.IsReleased && !successor.IsTraveling)
        {
            var party = successor.PartyBelongedTo ?? MobilePartyHelper.CreateNewClanMobileParty(successor, clan);
            if (party != null && party.LeaderHero != successor)
                party.ChangePartyLeader(successor);
        }

        clan.SetLeader(successor);

        // the leader-changed log entry reads the old leader, so name the former (retired) leader if there is one
        var formerLeader = clan.Heroes.FirstOrDefault(hero => hero.IsDisabled) ?? successor;
        CampaignEventDispatcher.Instance.OnClanLeaderChanged(formerLeader, successor);
    }

    private static Clan FindHomeClan(Hero hero, Clan closing)
    {
        bool Usable(Clan candidate) =>
            candidate != null && candidate != closing && !candidate.IsEliminated && !candidate.IsBanditFaction;

        if (Usable(hero.Father?.Clan)) return hero.Father.Clan;
        if (Usable(hero.Mother?.Clan)) return hero.Mother.Clan;
        if (Usable(hero.Spouse?.Clan)) return hero.Spouse.Clan;

        var rulingClan = closing.Kingdom?.RulingClan;
        if (Usable(rulingClan)) return rulingClan;

        // the closing clan can be the ruling clan and its only relatives inside it, so fall back to the
        // strongest other lord clan, one of the same kingdom and with a leader first
        return (closing.Kingdom?.Clans ?? Enumerable.Empty<Clan>())
            .Concat(Clan.NonBanditFactions)
            .Where(candidate => Usable(candidate) && !candidate.IsMinorFaction)
            .OrderByDescending(candidate => candidate.Kingdom == closing.Kingdom)
            .ThenByDescending(candidate => candidate.Leader is { IsAlive: true, IsDisabled: false })
            .ThenByDescending(candidate => candidate.Tier)
            .FirstOrDefault();
    }
}