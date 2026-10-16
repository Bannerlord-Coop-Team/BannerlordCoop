using GameInterface.Services.Heroes.Extensions;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans;

public interface ILeaderlessClanResolver
{
    /// <summary>
    /// Gives a kingdom clan without a usable leader a new one, or destroys the clan when nobody in it can lead.
    /// </summary>
    void Resolve(Clan clan);

    /// <summary>Resolves every leaderless kingdom clan in a loaded save.</summary>
    /// <returns>How many clans were resolved.</returns>
    int RepairLoadedClans();
}

/// <summary>
/// A kingdom clan whose leader is missing or disabled breaks every kingdom screen calculation that asks
/// each clan leader for relations (#4166: the settlement claim likelihood threw every frame and the
/// kingdom screen stayed black for the whole kingdom). This happened after a player retired with no heir.
/// The clan gets an adult member as leader, or is destroyed like a player clan that ends without heirs.
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
        if (successor != null)
        {
            logger.Warning("Kingdom clan {ClanId} had no leader, appointing {HeroId}", clan.StringId, successor.StringId);
            // ChangeClanLeaderAction hands the old leader's gold over and throws when there is no old leader
            clan.SetLeader(successor);
            return;
        }

        logger.Warning("Kingdom clan {ClanId} had no leader and nobody who can lead it, destroying it", clan.StringId);
        DestroyClanAction.Apply(clan);
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

    internal static bool NeedsLeader(Clan clan) =>
        clan != null &&
        !clan.IsEliminated &&
        !clan.IsBanditFaction &&
        clan.Kingdom != null &&
        (clan.Leader == null || clan.Leader.IsDisabled);

    // Same bar as an heir, but not limited by where the hero is (a prisoner can lead a clan).
    // AI family is preferred over other players, matching the retirement successor order.
    private static Hero FindSuccessor(Clan clan) =>
        clan.Heroes
            .Where(hero =>
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
}