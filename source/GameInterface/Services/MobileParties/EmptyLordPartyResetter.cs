using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;

public interface IEmptyLordPartyResetter
{
    /// <returns>How many lords were reset.</returns>
    int ResetEmptyLordParties();
}

/// <summary>
/// A save can hold AI lord parties with no healthy men outside any battle, for example after a
/// broken battle was force-ended. An empty party has nothing to do, so the lord stands on the map
/// for good. On load the lord gets the game's own "escaped after defeat" handling instead: the
/// empty party is destroyed, the lord becomes a fugitive and later returns in a settlement to
/// raise a new party. Nobody is killed and player parties are never touched.
/// </summary>
internal sealed class EmptyLordPartyResetter : IEmptyLordPartyResetter
{
    private readonly ILogger logger;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;

    public EmptyLordPartyResetter(ILogger logger, IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.logger = logger;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
    }

    public int ResetEmptyLordParties()
    {
        if (Campaign.Current?.CampaignObjectManager == null)
            return 0;

        var playerObjectIds = new HashSet<string>(playerManager.Players
            .SelectMany(player => new[] { player.MobilePartyId, player.HeroId })
            .Where(id => !string.IsNullOrEmpty(id)));

        var stranded = Campaign.Current.CampaignObjectManager.MobileParties
            .Where(party => IsStrandedLordParty(party) && !IsPlayerOwned(party, playerObjectIds))
            .ToList();

        var resetCount = 0;
        foreach (var party in stranded)
        {
            var lord = party.LeaderHero;
            try
            {
                Reset(party, lord);
                resetCount++;
                logger.Warning(
                    "Reset AI lord {HeroId} whose party {PartyId} had no healthy men after load",
                    lord.StringId,
                    party.StringId);
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to reset AI lord {HeroId} with an empty party", lord?.StringId);
            }
        }

        return resetCount;
    }

    internal static bool IsStrandedLordParty(MobileParty party)
    {
        if (party == null || !party.IsActive || !party.IsLordParty || party.IsMainParty || party.IsDisbanding)
            return false;

        var lord = party.LeaderHero;
        if (lord == null || !lord.IsAlive || lord.IsPrisoner)
            return false;

        return party.MapEvent == null && party.MemberRoster.TotalHealthyCount == 0;
    }

    private bool IsPlayerOwned(MobileParty party, ISet<string> playerObjectIds)
    {
        if (objectManager.TryGetId(party, out var partyId) && playerObjectIds.Contains(partyId))
            return true;

        return objectManager.TryGetId(party.LeaderHero, out var heroId) && playerObjectIds.Contains(heroId);
    }

    private static void Reset(MobileParty party, Hero lord)
    {
        // Let go of anyone held in the empty party, then anyone travelling with the lord.
        foreach (var element in party.PrisonRoster.GetTroopRoster().ToList())
        {
            if (element.Character?.HeroObject is Hero prisoner && prisoner.IsAlive && prisoner.IsPrisoner)
                EndCaptivityAction.ApplyByEscape(prisoner, null, false);
        }

        foreach (var element in party.MemberRoster.GetTroopRoster().ToList())
        {
            if (element.Character?.HeroObject is Hero member && member != lord && member.IsAlive)
                MakeHeroFugitiveAction.Apply(member, false);
        }

        var army = party.Army;
        if (army != null)
        {
            if (army.LeaderParty == party)
                DisbandArmyAction.ApplyByArmyLeaderIsDead(army);
            else
                party.Army = null;
        }

        MakeHeroFugitiveAction.Apply(lord, false);
    }
}