using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Interfaces;

public interface IMapEventResultsInterface : IGameAbstraction
{
    public NetworkPlayerLootData PackPlayerLootData(PlayerLootData playerLootData);
    public PlayerLootData UnpackPlayerLootData(NetworkPlayerLootData playerLootData);
    public void CalculateAndCommitMapEventResults(MapEvent mapEvent, out NetworkPlayerLootData networkPlayerLootData);
}

public class MapEventResultsInterface : IMapEventResultsInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventResultsInterface>();
    private readonly IObjectManager objectManager;

    public MapEventResultsInterface(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public NetworkPlayerLootData PackPlayerLootData(PlayerLootData playerLootData)
    {
        var lootedItems = new Dictionary<string, ItemRosterElement[]>();

        GameThread.RunSafe(() =>
        {
            foreach (var playerLootedItems in playerLootData.LootedItems)
            {
                if (!objectManager.TryGetIdWithLogging(playerLootedItems.Key, out var mapEventPartyId)) continue;

                lootedItems[mapEventPartyId] = playerLootedItems.Value._data;
            }
        });

        return new NetworkPlayerLootData(lootedItems);
    }

    public PlayerLootData UnpackPlayerLootData(NetworkPlayerLootData networkPlayerLootData)
    {
        var lootedItems = new Dictionary<MapEventParty, ItemRoster>();

        GameThread.RunSafe(() =>
        {
            foreach (var playerLootedItems in networkPlayerLootData.LootedItems)
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(playerLootedItems.Key, out var mapEventParty)) continue;

                using (new AllowedThread())
                {
                    lootedItems[mapEventParty] = new ItemRoster();
                    lootedItems[mapEventParty].Add(playerLootedItems.Value);
                }
            }
        });

        return new PlayerLootData(lootedItems);
    }

    public void CalculateAndCommitMapEventResults(MapEvent mapEvent, out NetworkPlayerLootData networkPlayerLootData)
    {
        var playerLootData = new PlayerLootData(new());

        GameThread.RunSafe(() =>
        {
            if (mapEvent.BattleState == BattleState.AttackerVictory || mapEvent.BattleState == BattleState.DefenderVictory)
            {
                MBList<MapEventParty> defeatedParties = mapEvent.GetMapEventSide(mapEvent.DefeatedSide).Parties.ToMBList<MapEventParty>();
                MBList<MapEventParty> winnerParties = mapEvent.GetMapEventSide(mapEvent.WinningSide).Parties.ToMBList<MapEventParty>();

                List<MapEventParty> winnerPlayerParties = winnerParties.FindAll(x => x.Party.MobileParty.IsPlayerParty());
                bool winningSideIncludesPlayers = winnerPlayerParties.Count > 0;

                foreach (var winnerPlayerParty in winnerPlayerParties)
                {
                    playerLootData.LootedItems[winnerPlayerParty] = new();
                }

                // Replace the following check to just check if its a naval map event. Only used for MapEvents involving players
                // mapEvent.IsPlayerMapEvent && PlayerEncounter.Current.IsNavalEncounterFinishedWithDisengage
                if (mapEvent.IsNavalMapEvent)
                {
                    mapEvent.LootDefeatedPartyShips(winnerParties, defeatedParties);
                }
                else
                {
                    LootDefeatedPartyCasualties(mapEvent, winnerParties, defeatedParties, winnerPlayerParties, winningSideIncludesPlayers, playerLootData.LootedItems);
                    LootDefeatedPartyItems(winnerParties, defeatedParties, playerLootData.LootedItems);
                    mapEvent.LootDefeatedPartyPrisoners(winnerParties, defeatedParties);
                    mapEvent.LootDefeatedPartyShips(winnerParties, defeatedParties);
                    mapEvent.CaptureDefeatedPartyMembers(winnerParties, defeatedParties);
                }

                // Need to patch the gold change to display plunder message
                mapEvent.CommitCalculatedMapEventResults();
            }
            mapEvent._mapEventResultsApplied = true;
        });

        networkPlayerLootData = PackPlayerLootData(playerLootData);
    }

    private void LootDefeatedPartyCasualties(
        MapEvent mapEvent,
        MBReadOnlyList<MapEventParty> winnerParties,
        MBReadOnlyList<MapEventParty> defeatedParties,
        List<MapEventParty> winnerPlayerParties,
        bool winningSideIncludesPlayers,
        Dictionary<MapEventParty, ItemRoster> playerLootRosters)
    {
        float aiTradePenalty = Campaign.Current.Models.BattleRewardModel.GetAITradePenalty();

        Dictionary<MapEventParty, float> playerLootFactors = new();
        if (winningSideIncludesPlayers)
        {
            foreach (var playerParty in winnerPlayerParties)
            {
                playerLootFactors[playerParty] = float.MinValue;
            }
        }
        
        foreach (MapEventParty defeatedParty in defeatedParties)
        {
            bool hasCasualties = defeatedParty.DiedInBattle.Count > 0 || defeatedParty.WoundedInBattle.Count > 0;
            if (!hasCasualties) continue;

            PartyBase defeatedPartyBase = defeatedParty.Party;

            MBReadOnlyList<KeyValuePair<MapEventParty, float>> lootCasualtyChances =
                    Campaign.Current.Models.BattleRewardModel.GetLootCasualtyChances(winnerParties, defeatedPartyBase);

            if (lootCasualtyChances.Count <= 0) continue;

            if (winningSideIncludesPlayers)
            {
                foreach (var playerParty in winnerPlayerParties)
                {
                    playerLootFactors[playerParty] = lootCasualtyChances.Find(x => x.Key == playerParty).Value;
                }
            }

            // Process dead troops
            for (int i = defeatedParty.DiedInBattle.Count - 1; i >= 0; i--)
            {
                CharacterObject character = defeatedParty.DiedInBattle.GetCharacterAtIndex(i);
                int troopCount = defeatedParty.DiedInBattle.GetElementNumber(i);

                for (int j = 0; j < troopCount; j++)
                {
                    MapEventParty lootReceiver = MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances(lootCasualtyChances);
                    if (lootReceiver == null) continue;

                    mapEvent.LootCasualtyCharacter(
                        character,
                        lootReceiver,
                        defeatedParty,
                        aiTradePenalty,
                        winnerPlayerParties.Contains(lootReceiver)
                            ? MBRandom.RoundRandomized(playerLootFactors[lootReceiver])
                            : int.MinValue,
                        playerLootRosters[lootReceiver]);
                }
            }

            // Process wounded troops
            for (int i = defeatedParty.WoundedInBattle.Count - 1; i >= 0; i--)
            {
                CharacterObject character = defeatedParty.WoundedInBattle.GetCharacterAtIndex(i);
                int troopCount = defeatedParty.WoundedInBattle.GetElementNumber(i);

                for (int j = 0; j < troopCount; j++)
                {
                    MapEventParty lootReceiver = MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances(lootCasualtyChances);
                    if (lootReceiver == null) continue;

                    mapEvent.LootCasualtyCharacter(
                        character,
                        lootReceiver,
                        defeatedParty,
                        aiTradePenalty,
                        winnerPlayerParties.Contains(lootReceiver)
                            ? MBRandom.RoundRandomized(playerLootFactors[lootReceiver])
                            : int.MinValue,
                        playerLootRosters[lootReceiver]);
                }
            }

            if (winningSideIncludesPlayers)
            {
                foreach (var playerParty in winnerPlayerParties)
                {
                    if (playerLootRosters[playerParty].Count <= 0) continue;

                    // Gives roguery XP when raiding caravans or attacking villagers
                    CampaignEventDispatcher.Instance.OnLootDistributedToParty(playerParty.Party, defeatedPartyBase, playerLootRosters[playerParty]);

                    //playerBattleParty.RosterToReceiveLootItems.Add(playerLootRoster[playerParty]);
                }
            }
        }
    }

    private void LootDefeatedPartyItems(
        MBReadOnlyList<MapEventParty> winnerParties,
        MBReadOnlyList<MapEventParty> defeatedParties,
        Dictionary<MapEventParty, ItemRoster> playerLootRosters)
    {
        foreach (MapEventParty defeatedParty in defeatedParties)
        {
            Dictionary<MapEventParty, ItemRoster> lootByWinnerParty = new Dictionary<MapEventParty, ItemRoster>();

            PartyBase defeatedPartyBase = defeatedParty.Party;

            MBList<KeyValuePair<MapEventParty, float>> lootChances =
                Campaign.Current.Models.BattleRewardModel.GetLootItemChancesForWinnerParties(winnerParties, defeatedPartyBase);

            List<ItemRosterElement> lootableItems =
                defeatedPartyBase.ItemRoster
                    .Where(x =>
                        !x.EquipmentElement.Item.NotMerchandise &&
                        !x.EquipmentElement.IsQuestItem &&
                        !x.EquipmentElement.Item.IsBannerItem)
                    .ToList();

            if (lootChances.Count > 0)
            {
                foreach (ItemRosterElement item in lootableItems)
                {
                    for (int i = 0; i < item.Amount; i++)
                    {
                        MapEventParty winnerParty = MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances(lootChances.ToMBList());
                        if (winnerParty == null) continue;

                        if (!lootByWinnerParty.TryGetValue(winnerParty, out ItemRoster winnerLootRoster))
                        {
                            winnerLootRoster = new ItemRoster();

                            lootByWinnerParty.Add(
                                winnerParty,
                                winnerLootRoster);
                        }

                        // Add item to winner's temporary loot roster
                        winnerLootRoster.AddToCounts(item.EquipmentElement, 1);

                        // Remove item from defeated party
                        defeatedPartyBase.ItemRoster.AddToCounts(item.EquipmentElement, -1);
                    }
                }

                // Distribute accumulated loot
                foreach (var pair in lootByWinnerParty)
                {
                    MapEventParty winnerParty = pair.Key;
                    ItemRoster lootRoster = pair.Value;

                    if (lootRoster.Count <= 0) continue;

                    if (playerLootRosters.ContainsKey(winnerParty))
                    {
                        playerLootRosters[winnerParty].Add(lootRoster);
                    }
                    else
                    {
                        winnerParty.RosterToReceiveLootItems.Add(lootRoster);
                    }

                    // Gives roguery XP when raiding caravans or attacking villagers
                    CampaignEventDispatcher.Instance.OnLootDistributedToParty(winnerParty.Party, defeatedPartyBase, lootRoster);
                }

                continue;
            }
        }

        // Applies some extra logic to the looted roster. e.g. Metallurgy perk reducing chance of getting negative modifiers on loot
        foreach (MapEventParty winnerParty in winnerParties)
        {
            if (playerLootRosters.ContainsKey(winnerParty))
            {
                CampaignEventDispatcher.Instance.OnCollectLootItems(winnerParty.Party, playerLootRosters[winnerParty]);
            }
            else if (winnerParty.RosterToReceiveLootItems.Count > 0)
            {
                CampaignEventDispatcher.Instance.OnCollectLootItems(winnerParty.Party, winnerParty.RosterToReceiveLootItems);
            }
        }
    }
}
