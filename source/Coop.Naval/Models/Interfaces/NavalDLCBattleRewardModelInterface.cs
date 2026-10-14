using Coop.Naval.CoopSession.Interfaces;
using GameInterface.CoopSessionData;
using GameInterface.Services;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Interfaces;
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

public interface INavalDLCBattleRewardModelInterface : IGameAbstraction
{
    MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> DistributeDefeatedPartyShipsAmongWinners(NavalDLCBattleRewardModel model, MapEventParty mainParty, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties);
    void CalculatePlayerFigureheadShare(MapEvent mapEvent, MBReadOnlyList<MapEventParty> defeatedParties, PartyBase defeatedLeaderParty);
}

public class NavalDLCBattleRewardModelInterface : INavalDLCBattleRewardModelInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerPartyLookup playerPartyLookup;
    private readonly ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface;

    public NavalDLCBattleRewardModelInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IPlayerPartyLookup playerPartyLookup,
        ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerPartyLookup = playerPartyLookup;
        this.sessionNavalPlayerDataInterface = sessionNavalPlayerDataInterface;
    }

    public MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> DistributeDefeatedPartyShipsAmongWinners(NavalDLCBattleRewardModel model, MapEventParty mainParty, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
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

        // Replace PartyBase.MainParty usage with the winning player's party
        if (lootableShips.Count > 0)
        {
            lootableShips.Shuffle();
            int mainPartyShipCount = distributedShips.Count(x => x.Value == mainParty);
            if (lootableShips.Count + mainPartyShipCount > 25)
            {
                lootableShips = lootableShips.Take(25 - mainPartyShipCount).ToMBList();
            }
            int totalContribution = winnerParties.Sum(x => x.ContributionToBattle);
            foreach (Ship ship in lootableShips)
            {
                if (MBRandom.RandomInt(totalContribution) >= mainParty.ContributionToBattle) break;

                distributedShips[ship] = mainParty;
            }
        }

        return distributedShips.ToMBList();
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
            if (!playerPartyLookup.TryGetPlayerHero(mobileParty, out var playerHero)) continue;

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
