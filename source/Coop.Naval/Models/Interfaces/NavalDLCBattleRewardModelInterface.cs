using Coop.Naval.CoopSession.Interfaces;
using GameInterface.CoopSessionData;
using GameInterface.Services;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using NavalDLC.GameComponents;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Naval.Models.Interfaces;

/// <summary>[Server] Vanilla's naval battle rewards for the main party, given to every winning player instead.</summary>
public interface INavalDLCBattleRewardModelInterface : IGameAbstraction
{
    /// <summary>Vanilla's ship distribution, with the main party's share of the leftover ships split among <paramref name="playerParties"/>.</summary>
    MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> DistributeDefeatedPartyShipsAmongWinners(NavalDLCBattleRewardModel model, List<MapEventParty> playerParties, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties);
    void CalculatePlayerFigureheadShare(MapEvent mapEvent, MBReadOnlyList<MapEventParty> defeatedParties, PartyBase defeatedLeaderParty);
}

/// <inheritdoc cref="INavalDLCBattleRewardModelInterface"/>
public class NavalDLCBattleRewardModelInterface : INavalDLCBattleRewardModelInterface
{
    // Vanilla's cap on the ships the main party's loot screen offers.
    private const int MaxLootShips = 25;

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface;

    public NavalDLCBattleRewardModelInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.sessionNavalPlayerDataInterface = sessionNavalPlayerDataInterface;
    }

    public MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> DistributeDefeatedPartyShipsAmongWinners(NavalDLCBattleRewardModel model, List<MapEventParty> playerParties, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
    {
        Dictionary<Ship, MapEventParty> distributedShips = new();
        MBList<Ship> lootableShips = new();
        foreach (Ship ship in shipsToLoot)
        {
            distributedShips.Add(ship, null);
            if (MBRandom.RandomFloat < 0.5f)
            {
                if (ship.CanEquipFigurehead)
                {
                    ship.ChangeFigurehead(null);
                }
                lootableShips.Add(ship);
            }
        }

        List<MapEventParty> navalWinnerParties = winnerParties.Where(x => x.Party.IsMobile && x.Party.MobileParty.PartyComponent.CanHaveNavalNavigationCapability && !x.Party.MobileParty.IsPatrolParty).ToList();
        if (navalWinnerParties.Any())
        {
            float totalScore = navalWinnerParties.Sum(x => model.PartyLootShipScore(x));
            List<MapEventParty> orderedWinnerParties = navalWinnerParties.OrderBy(x => x.Party.Ships.Count + (1f - (model.PartyLootShipScore(x) / totalScore))).ToList();
            List<MapEventParty> extraWinnerParties = new();
            if (lootableShips.Count < orderedWinnerParties.Count)
            {
                extraWinnerParties = orderedWinnerParties.GetRange(lootableShips.Count, orderedWinnerParties.Count - lootableShips.Count);
                orderedWinnerParties.RemoveRange(lootableShips.Count, orderedWinnerParties.Count - lootableShips.Count);
            }
            orderedWinnerParties = orderedWinnerParties.OrderByDescending(x => model.PartyLootShipScore(x)).ToList();
            orderedWinnerParties.AddRange(extraWinnerParties.OrderByDescending(x => model.PartyLootShipScore(x)));

            bool shipLooted = true;
            while (shipLooted && lootableShips.Count > 0)
            {
                shipLooted = false;
                foreach (MapEventParty winnerParty in orderedWinnerParties)
                {
                    MBList<Ship> partyShips = winnerParty.Ships.ToMBList();
                    partyShips.AddRange(distributedShips.Where(x => x.Value == winnerParty).Select(x => x.Key));
                    Ship shipToLoot = model.GetShipToLootForWinnerParty(winnerParty, partyShips, lootableShips);
                    if (shipToLoot != null)
                    {
                        shipLooted = true;
                        distributedShips[shipToLoot] = winnerParty;
                        lootableShips.Remove(shipToLoot);
                    }
                    if (lootableShips.Count == 0) break;
                }
                if (lootableShips.Count > 0)
                {
                    orderedWinnerParties = orderedWinnerParties.OrderByDescending(x => model.PartyLootShipScore(x)).ToList();
                }
            }
        }

        // Vanilla gives PartyBase.MainParty each leftover ship while the contribution roll lands on its share; a roll on any
        // winning player's share gives it that player, so players winning together keep what one player would alone
        if (lootableShips.Count > 0)
        {
            lootableShips.Shuffle();
            int totalContribution = winnerParties.Sum(x => x.ContributionToBattle);
            foreach (Ship ship in lootableShips)
            {
                MapEventParty looter = PlayerAtContributionRoll(playerParties, MBRandom.RandomInt(totalContribution));
                if (looter == null) break;
                if (distributedShips.Count(x => x.Value == looter) >= MaxLootShips) continue;

                distributedShips[ship] = looter;
            }
        }

        return distributedShips.ToMBList();
    }

    // The winning player whose part of the winners' total contribution the roll falls in, or null for an AI winner's part.
    private static MapEventParty PlayerAtContributionRoll(List<MapEventParty> playerParties, int roll)
    {
        foreach (MapEventParty playerParty in playerParties)
        {
            if (roll < playerParty.ContributionToBattle) return playerParty;

            roll -= playerParty.ContributionToBattle;
        }

        return null;
    }

    public void CalculatePlayerFigureheadShare(MapEvent mapEvent, MBReadOnlyList<MapEventParty> defeatedParties, PartyBase defeatedLeaderParty)
    {
        // Vanilla only sets this for the main party
        if (mapEvent._playerFigureheadCalculated) return;
        if (Campaign.Current.Models.BattleRewardModel is not NavalDLCBattleRewardModel model) return;

        mapEvent._playerFigureheadCalculated = true;

        foreach (MapEventParty winnerParty in mapEvent.GetMapEventSide(mapEvent.WinningSide).Parties)
        {
            MobileParty mobileParty = winnerParty.Party.MobileParty;
            if (mobileParty == null || !mobileParty.IsPlayerParty()) continue;
            if (!playerManager.TryGetPlayer(mobileParty, out var player)) continue;
            if (!objectManager.TryGetObjectWithLogging(player.HeroId, out Hero playerHero)) continue;

            Figurehead figurehead = GetFigureheadLoot(model, playerHero, defeatedParties, defeatedLeaderParty);
            if (figurehead != null)
            {
                sessionNavalPlayerDataInterface.UnlockFigurehead(playerHero, figurehead);
            }
        }
    }

    // Same as NavalDLCBattleRewardModel.GetFigureheadLoot with the player's own loot time and unlocked figureheads
    private Figurehead GetFigureheadLoot(NavalDLCBattleRewardModel model, Hero playerHero, MBReadOnlyList<MapEventParty> defeatedParties, PartyBase defeatedSideLeaderParty)
    {
        if (!objectManager.TryGetId(playerHero, out var playerHeroId)) return null;
        if (sessionNavalPlayerDataInterface.GetLastFigureheadLootTime(playerHeroId).ElapsedDaysUntilNow < 8f) return null;

        IEnumerable<Hero> heroes = defeatedParties.Where(x => x.Party.LeaderHero != null).Select(x => x.Party.LeaderHero);
        if (MBRandom.RandomFloat > model.GetFigureheadDropChanceForHeroes(heroes)) return null;

        coopSessionProvider.CoopSession.NavalPlayerData.PlayerUnlockedFigureHeads.TryGetValue(playerHeroId, out var unlockedFigureheads);
        List<(Figurehead, float)> figureheads = new();
        foreach (MapEventParty defeatedParty in defeatedParties)
        {
            foreach (Ship ship in defeatedParty.Ships)
            {
                if (ship.Figurehead == null || unlockedFigureheads?.Contains(ship.Figurehead.StringId) == true) continue;

                if (defeatedParty.Party == defeatedSideLeaderParty && defeatedSideLeaderParty.MobileParty?.Army?.LeaderParty == defeatedSideLeaderParty.MobileParty)
                {
                    figureheads.Add((ship.Figurehead, 0.2f));
                    continue;
                }
                figureheads.Add((ship.Figurehead, 0.1f));
            }
        }
        return MBRandom.ChooseWeighted(figureheads);
    }
}
