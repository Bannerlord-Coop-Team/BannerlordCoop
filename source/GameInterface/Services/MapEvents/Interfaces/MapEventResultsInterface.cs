using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Patches;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
    private readonly ITroopRosterInterface troopRosterInterface;

    public MapEventResultsInterface(IObjectManager objectManager, ITroopRosterInterface troopRosterInterface)
    {
        this.objectManager = objectManager;
        this.troopRosterInterface = troopRosterInterface;
    }

    public NetworkPlayerLootData PackPlayerLootData(PlayerLootData playerLootData)
    {
        var lootedItems = new Dictionary<string, ItemRosterElement[]>();
        var lootedMembers = new Dictionary<string, TroopRosterData>();
        var lootedPrisoners = new Dictionary<string, TroopRosterData>();

        GameThread.RunSafe(() =>
        {
            foreach (var playerLootedItems in playerLootData.LootedItems)
            {
                if (!objectManager.TryGetIdWithLogging(playerLootedItems.Key, out var mapEventPartyId)) continue;

                lootedItems[mapEventPartyId] = playerLootedItems.Value._data;
            }

            foreach (var playerLootedMembers in playerLootData.LootedMembers)
            {
                if (!objectManager.TryGetIdWithLogging(playerLootedMembers.Key, out var mapEventPartyId)) continue;

                lootedMembers[mapEventPartyId] = troopRosterInterface.PackTroopRosterData(playerLootedMembers.Value);
            }

            foreach (var playerLootedPrisoners in playerLootData.LootedPrisoners)
            {
                if (!objectManager.TryGetIdWithLogging(playerLootedPrisoners.Key, out var mapEventPartyId)) continue;

                lootedPrisoners[mapEventPartyId] = troopRosterInterface.PackTroopRosterData(playerLootedPrisoners.Value);
            }
        });

        return new NetworkPlayerLootData(lootedItems, lootedMembers, lootedPrisoners);
    }

    public PlayerLootData UnpackPlayerLootData(NetworkPlayerLootData networkPlayerLootData)
    {
        var lootedItems = new Dictionary<MapEventParty, ItemRoster>();
        var lootedMembers = new Dictionary<MapEventParty, TroopRoster>();
        var lootedPrisoners = new Dictionary<MapEventParty, TroopRoster>();
        var networkLootedItems = networkPlayerLootData.LootedItems ?? new();
        var networkLootedMembers = networkPlayerLootData.LootedMembers ?? new();
        var networkLootedPrisoners = networkPlayerLootData.LootedPrisoners ?? new();

        GameThread.RunSafe(() =>
        {
            // Pack looted items
            foreach (var playerLootedItems in networkLootedItems)
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(playerLootedItems.Key, out var mapEventParty)) continue;

                using (new AllowedThread())
                {
                    lootedItems[mapEventParty] = new ItemRoster();
                    lootedItems[mapEventParty].Add(playerLootedItems.Value);
                }
            }

            // Pack looted members (prisoners freed from defeated party)
            foreach (var playerLootedMembers in networkLootedMembers)
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(playerLootedMembers.Key, out var mapEventParty)) continue;

                using (new AllowedThread())
                {
                    lootedMembers[mapEventParty] = new TroopRoster();
                    foreach (var troopRosterElement in troopRosterInterface.UnpackTroopRosterData(playerLootedMembers.Value))
                    {
                        lootedMembers[mapEventParty].Add(troopRosterElement);
                    }
                }
            }

            // Pack looted prisoners (troops taken prisoner from defeated party)
            foreach (var playerLootedPrisoners in networkLootedPrisoners)
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(playerLootedPrisoners.Key, out var mapEventParty)) continue;

                using (new AllowedThread())
                {
                    lootedPrisoners[mapEventParty] = new TroopRoster();
                    foreach (var troopRosterElement in troopRosterInterface.UnpackTroopRosterData(playerLootedPrisoners.Value))
                    {
                        lootedPrisoners[mapEventParty].Add(troopRosterElement);
                    }
                }
            }
        });

        return new PlayerLootData(lootedItems, lootedMembers, lootedPrisoners);
    }

    public void CalculateAndCommitMapEventResults(MapEvent mapEvent, out NetworkPlayerLootData networkPlayerLootData)
    {
        var playerLootData = new PlayerLootData(new(), new(), new());

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
                    playerLootData.LootedMembers[winnerPlayerParty] = new();
                    playerLootData.LootedPrisoners[winnerPlayerParty] = new();
                }

                // Replace the following check to just check if its a naval map event. Only used for MapEvents involving players
                // mapEvent.IsPlayerMapEvent && PlayerEncounter.Current.IsNavalEncounterFinishedWithDisengage
                if (mapEvent.IsNavalMapEvent) // TODO Change to only do this for naval encounters where ships disengage rather than total victories(?)
                {
                    mapEvent.LootDefeatedPartyShips(winnerParties, defeatedParties); // TODO
                }
                else
                {
                    LootDefeatedPartyCasualties(winnerParties, defeatedParties, winnerPlayerParties, winningSideIncludesPlayers, playerLootData.LootedItems);
                    LootDefeatedPartyItems(winnerParties, defeatedParties, playerLootData.LootedItems);
                    LootDefeatedPartyPrisoners(winnerParties, defeatedParties, playerLootData.LootedMembers);
                    mapEvent.LootDefeatedPartyShips(winnerParties, defeatedParties); // TODO
                    CaptureDefeatedPartyMembers(mapEvent, winnerParties, defeatedParties, playerLootData.LootedPrisoners);
                }

                // Need to patch the gold change to display plunder message
                mapEvent.CommitCalculatedMapEventResults();
            }
            mapEvent._mapEventResultsApplied = true;
        });

        networkPlayerLootData = PackPlayerLootData(playerLootData);
    }

    // Needs extra arguments because vanilla seems to only allow the player to loot items from enemy casualties
    // as opposed to looting from existing item rosters (what both player and AI parties are allowed to do).
    // This is an extended version to work with multiple involved winning players
    private void LootDefeatedPartyCasualties(
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

                    bool playerReceivesLoot = playerLootRosters.TryGetValue(lootReceiver, out ItemRoster playerLootRoster);

                    LootCasualtyCharacter(
                        character,
                        lootReceiver,
                        defeatedParty,
                        aiTradePenalty,
                        playerReceivesLoot
                            ? MBRandom.RoundRandomized(playerLootFactors[lootReceiver])
                            : int.MinValue,
                        playerLootRoster);
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

                    bool playerReceivesLoot = playerLootRosters.TryGetValue(lootReceiver, out ItemRoster playerLootRoster);

                    LootCasualtyCharacter(
                        character,
                        lootReceiver,
                        defeatedParty,
                        aiTradePenalty,
                        playerReceivesLoot
                            ? MBRandom.RoundRandomized(playerLootFactors[lootReceiver])
                            : int.MinValue,
                        playerLootRoster);
                }
            }

            if (winningSideIncludesPlayers)
            {
                foreach (var playerParty in winnerPlayerParties)
                {
                    if (playerLootRosters[playerParty].Count <= 0) continue;

                    // Gives roguery XP when raiding caravans or attacking villagers
                    CampaignEventDispatcher.Instance.OnLootDistributedToParty(playerParty.Party, defeatedPartyBase, playerLootRosters[playerParty]);

                    // Added client-side by broadcasting the player loot rosters
                    //playerBattleParty.RosterToReceiveLootItems.Add(playerLootRoster[playerParty]);
                }
            }
        }
    }

    private void LootCasualtyCharacter(CharacterObject casualtyCharacter, MapEventParty winnerParty, MapEventParty defeatedParty, float aiTradePenalty, int maxLootedItemsPerBodyForMainParty, ItemRoster mainPartyLootFromCasualties)
    {
        Hero leaderHero = winnerParty.Party.LeaderHero;
        if (leaderHero == null) return;

        float expectedLootedItemValueFromCasualty = Campaign.Current.Models.BattleRewardModel.GetExpectedLootedItemValueFromCasualty(leaderHero, casualtyCharacter);
        if (expectedLootedItemValueFromCasualty.ApproximatelyEqualsTo(0f, 1E-05f)) return;

        if (!leaderHero.IsPlayerHero())
        {
            int num = (int)((float)MathF.Round(expectedLootedItemValueFromCasualty) * aiTradePenalty);
            if (num > 0)
            {
                winnerParty.Party.MobileParty.PartyTradeGold += num;
                SkillLevelingManager.OnAIPartyLootCasualties(num, leaderHero, defeatedParty.Party);
                return;
            }
        }
        else if (maxLootedItemsPerBodyForMainParty > 0)
        {
            List<EquipmentElement> list = new List<EquipmentElement>();
            for (int i = 0; i < maxLootedItemsPerBodyForMainParty; i++)
            {
                EquipmentElement lootedItem = Campaign.Current.Models.BattleRewardModel.GetLootedItemFromTroop(casualtyCharacter, expectedLootedItemValueFromCasualty);
                if (lootedItem.Item != null && !list.Exists((EquipmentElement x) => x.Item.Type == lootedItem.Item.Type))
                {
                    list.Add(lootedItem);
                    mainPartyLootFromCasualties.AddToCounts(lootedItem, 1);
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

    private void LootDefeatedPartyPrisoners(
        MBReadOnlyList<MapEventParty> winnerParties,
        MBReadOnlyList<MapEventParty> defeatedParties,
        Dictionary<MapEventParty, TroopRoster> playerLootMemberRosters)
    {
        foreach (MapEventParty defeatedParty in defeatedParties)
        {
            if (defeatedParty.Party.PrisonRoster.Count <= 0) continue;

            TroopRoster defeatedPrisonRoster = defeatedParty.Party.PrisonRoster;
            MBList<TroopRosterElement> prisoners = defeatedPrisonRoster.GetTroopRoster();

            for (int i = prisoners.Count - 1; i >= 0; i--)
            {
                TroopRosterElement prisonerEntry = prisoners[i];
                CharacterObject prisonerCharacter = prisonerEntry.Character;
                int prisonerCount = prisonerEntry.Number;

                MBReadOnlyList<KeyValuePair<MapEventParty, float>> lootPrisonerChances =
                        Campaign.Current.Models.BattleRewardModel.GetLootPrisonerChances(winnerParties, prisonerEntry);

                // Immediately remove non-hero prisoners from the defeated party roster
                if (!prisonerCharacter.IsHero)
                {
                    defeatedPrisonRoster.RemoveTroop(prisonerCharacter, prisonerCount, default(UniqueTroopDescriptor), 0);
                }

                if (lootPrisonerChances.Count > 0)
                {
                    for (int j = 0; j < prisonerCount; j++)
                    {
                        MapEventParty winnerParty = MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances(lootPrisonerChances);
                        TroopRoster winnerLootRoster = winnerParty?.RosterToReceiveLootMembers;

                        // No valid recipient roster
                        if (winnerLootRoster == null)
                        {
                            if (prisonerCharacter.IsHero)
                            {
                                EndCaptivityAction.ApplyByReleasedAfterBattle(prisonerCharacter.HeroObject);
                            }

                            continue;
                        }

                        // Handle hero prisoners
                        if (prisonerCharacter.IsHero)
                        {
                            bool shouldRelease = prisonerCharacter.HeroObject.IsPlayerHero();

                            if (!shouldRelease && winnerParty.Party.MobileParty != null && !winnerParty.Party.MobileParty.IsPlayerParty())
                            {
                                shouldRelease = !winnerLootRoster.OwnerParty.MapFaction.IsAtWarWith(prisonerCharacter.HeroObject.MapFaction);
                            }

                            if (shouldRelease)
                            {
                                EndCaptivityAction.ApplyByReleasedAfterBattle(prisonerCharacter.HeroObject);

                                continue;
                            }

                            // Remove hero from defeated prison roster
                            defeatedPrisonRoster.RemoveTroop(prisonerCharacter, prisonerCount, default(UniqueTroopDescriptor), 0);

                            if (playerLootMemberRosters.ContainsKey(winnerParty))
                            {
                                playerLootMemberRosters[winnerParty].AddToCounts(prisonerCharacter, 1, false, 0, 0, true, -1);
                            }
                            else
                            {
                                winnerParty.RosterToReceiveLootPrisoners.AddToCounts(prisonerCharacter, 1, false, 0, 0, true, -1);
                            }
                        }

                        // Add non-hero troops
                        else if (playerLootMemberRosters.ContainsKey(winnerParty))
                        {
                            playerLootMemberRosters[winnerParty].AddToCounts(prisonerCharacter, 1, false, 0, 0, true, -1);
                        }
                        else
                        {
                            winnerLootRoster.AddToCounts(prisonerCharacter, 1, false, 0, 0, true, -1);
                        }
                    }
                }

                // Nobody can receive these prisoners
                else if (prisonerCharacter.IsHero)
                {
                    EndCaptivityAction.ApplyByReleasedAfterBattle(prisonerCharacter.HeroObject);
                }
            }

            defeatedPrisonRoster.RemoveZeroCounts();
        }
    }

    private void CaptureDefeatedPartyMembers(
        MapEvent mapEvent,
        MBReadOnlyList<MapEventParty> winnerParties,
        MBReadOnlyList<MapEventParty> defeatedParties,
        Dictionary<MapEventParty, TroopRoster> playerLootPrisonerRosters)
    {
        // No prisoners are taken if one side retreated
        if (mapEvent.RetreatingSide != BattleSideEnum.None) return;

        // The coop player capture used to run as a prefix on native MapEvent.CaptureDefeatedPartyMembers,
        // which this reimplementation replaces — so it must be invoked explicitly. It runs FIRST because
        // capturing a player clears that party's roster on the server (PlayerCaptivityServerHandler), so
        // the loop below (which skips player heroes) cannot re-process or scatter the captured hero's men.
        PlayerStartCaptivityPatches.CaptureDefeatedPlayerHeroes(winnerParties, defeatedParties);

        MBList<KeyValuePair<MapEventParty, float>> woundedCaptureChances;
        MBList<KeyValuePair<MapEventParty, float>> healthyCaptureChances;

        Campaign.Current.Models.BattleRewardModel.GetCaptureMemberChancesForWinnerParties(mapEvent, winnerParties, out woundedCaptureChances, out healthyCaptureChances);
        float playerPartyMemberScatterChance = Campaign.Current.Models.BattleRewardModel.GetMainPartyMemberScatterChance();

        for (int i = defeatedParties.Count - 1; i >= 0; i--)
        {
            PartyBase defeatedParty = defeatedParties[i].Party;

            for (int j = defeatedParty.MemberRoster.Count - 1; j >= 0; j--)
            {
                TroopRosterElement troop = defeatedParty.MemberRoster.GetElementCopyAtIndex(j);
                if (troop.Number == 0) continue;

                CharacterObject character = troop.Character;

                // Process captured hero
                if (character.IsHero)
                {
                    Hero hero = character.HeroObject;

                    // Player heroes handled separately
                    if (hero.IsPlayerHero()) continue;

                    bool heroIsDead = hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInBattle 
                        || hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInLabor;

                    if (heroIsDead || hero.Occupation == Occupation.Special) continue;

                    // Remove party leader if captured/removed
                    if (defeatedParty.IsMobile && defeatedParty.LeaderHero == hero)
                    {
                        defeatedParty.MobileParty.RemovePartyLeader();
                    }

                    bool canBecomePrisoner = hero.CanBecomePrisoner();

                    bool playerPartyEscaped = defeatedParty.IsMobile && defeatedParty.MobileParty.IsPlayerParty() && MBRandom.RandomFloat <= playerPartyMemberScatterChance;

                    if (canBecomePrisoner && !playerPartyEscaped)
                    {
                        MBList<KeyValuePair<MapEventParty, float>> captureChances = hero.IsWounded ? woundedCaptureChances : healthyCaptureChances;

                        if (captureChances.Count > 0)
                        {
                            MapEventParty captorParty = MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances(captureChances);

                            if (captorParty != null &&
                                playerLootPrisonerRosters.TryGetValue(captorParty, out TroopRoster playerLootPrisonerRoster))
                            {
                                playerLootPrisonerRoster.AddToCounts(character, 1, false, 0, 0, true, -1);
                            }
                            else
                            {
                                TroopRoster captorPrisonRoster = captorParty?.RosterToReceiveLootPrisoners;

                                if (captorPrisonRoster != null)
                                {
                                    // Normal captor party
                                    if (captorPrisonRoster.OwnerParty != null)
                                    {
                                        TakePrisonerAction.Apply(captorPrisonRoster.OwnerParty, hero);
                                    }

                                    // Temporary roster only
                                    else
                                    {
                                        captorPrisonRoster.AddToCounts(character, 1, false, 0, 0, true, -1);

                                        defeatedParty.MemberRoster.AddToCountsAtIndex(j, -troop.Number, 0, 0, false);
                                    }
                                }
                            }
                        }
                    }

                    // Hero escaped instead of captured
                    if (defeatedParty.MemberRoster.GetElementCopyAtIndex(j).Number > 0 && hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
                    {
                        MakeHeroFugitiveAction.Apply(hero, false);
                    }
                }

                // Process captured regular troops
                else
                {
                    if (Campaign.Current.Models.BattleRewardModel.CanTroopBeTakenPrisoner(character))
                    {
                        // Add wounded troops
                        if (woundedCaptureChances.Count > 0)
                        {
                            for (int k = 0; k < troop.WoundedNumber; k++)
                            {
                                MapEventParty captorParty = MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances(woundedCaptureChances);

                                if (captorParty != null &&
                                    playerLootPrisonerRosters.TryGetValue(captorParty, out TroopRoster playerLootPrisonerRoster))
                                {
                                    playerLootPrisonerRoster.AddToCounts(character, 1, false, 0, 0, true, -1);
                                }
                                else
                                {
                                    TroopRoster captorPrisonRoster = captorParty?.RosterToReceiveLootPrisoners;

                                    if (captorPrisonRoster == null) continue;

                                    captorPrisonRoster.AddToCounts(character, 1, false, 0, 0, true, -1);
                                }
                            }
                        }

                        // Add healthy troops
                        if (healthyCaptureChances.Count > 0)
                        {
                            int healthyTroopCount =troop.Number - troop.WoundedNumber;

                            for (int k = 0; k < healthyTroopCount; k++)
                            {
                                MapEventParty captorParty = MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances(healthyCaptureChances);

                                if (captorParty != null &&
                                    playerLootPrisonerRosters.TryGetValue(captorParty, out TroopRoster playerLootPrisonerRoster))
                                {
                                    playerLootPrisonerRoster.AddToCounts(character, 1, false, 0, 0, true, -1);
                                }
                                else
                                {
                                    TroopRoster captorPrisonRoster = captorParty?.RosterToReceiveLootPrisoners;

                                    if (captorPrisonRoster == null) continue;

                                    captorPrisonRoster.AddToCounts(character, 1, false, 0, 0, true, -1);
                                }
                            }
                        }
                    }

                    // Remove troops from defeated party
                    defeatedParty.MemberRoster.AddToCountsAtIndex(j,  -troop.Number, -troop.WoundedNumber, 0, false);
                }
            }

            defeatedParty.MemberRoster.RemoveZeroCounts();
        }
    }
}
