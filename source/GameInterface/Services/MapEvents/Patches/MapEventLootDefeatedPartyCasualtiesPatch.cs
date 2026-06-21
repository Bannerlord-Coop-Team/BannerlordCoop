using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Replaces <see cref="MapEvent.LootDefeatedPartyCasualties"/> with a managed copy of the vanilla
/// body (private members are reached through the publicized assembly) so coop can control the loot
/// distribution path. The prefix returns <c>false</c> to skip the original.
/// </summary>
[HarmonyPatch(typeof(MapEvent), nameof(MapEvent.LootDefeatedPartyCasualties))]
internal class MapEventLootDefeatedPartyCasualtiesPatch
{
    [HarmonyPrefix]
    private static bool Prefix_LootDefeatedPartyCasualties(MapEvent __instance, MBReadOnlyList<MapEventParty> winnerParties, MBReadOnlyList<MapEventParty> defeatedParties)
    {
        float aITradePenalty = Campaign.Current.Models.BattleRewardModel.GetAITradePenalty();
        bool flag = __instance.IsPlayerMapEvent && __instance.PlayerSide == __instance.WinningSide;
        float f = float.MinValue;
        ItemRoster itemRoster = null;
        MapEventParty playerBattleParty = (flag ? winnerParties.Find((MapEventParty x) => x.Party == PartyBase.MainParty) : null);
        foreach (MapEventParty defeatedParty in defeatedParties)
        {
            if (defeatedParty.DiedInBattle.Count <= 0 && defeatedParty.WoundedInBattle.Count <= 0)
            {
                continue;
            }
            PartyBase party = defeatedParty.Party;
            MBReadOnlyList<KeyValuePair<MapEventParty, float>> lootCasualtyChances = Campaign.Current.Models.BattleRewardModel.GetLootCasualtyChances(winnerParties, party);
            if (flag)
            {
                if (playerBattleParty == null)
                {
                    playerBattleParty = lootCasualtyChances.Find((KeyValuePair<MapEventParty, float> x) => x.Key.Party == PartyBase.MainParty).Key;
                }
                itemRoster = new ItemRoster();
                f = lootCasualtyChances.Find((KeyValuePair<MapEventParty, float> x) => x.Key == playerBattleParty).Value;
            }
            if (lootCasualtyChances.Count <= 0)
            {
                continue;
            }
            CharacterObject characterObject = null;
            for (int num = defeatedParty.DiedInBattle.Count - 1; num >= 0; num--)
            {
                characterObject = defeatedParty.DiedInBattle.GetCharacterAtIndex(num);
                for (int num2 = 0; num2 < defeatedParty.DiedInBattle.GetElementNumber(num); num2++)
                {
                    MapEventParty mapEventParty = MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances(lootCasualtyChances);
                    if (mapEventParty != null)
                    {
                        __instance.LootCasualtyCharacter(characterObject, mapEventParty, defeatedParty, aITradePenalty, flag ? MBRandom.RoundRandomized(f) : int.MinValue, itemRoster);
                    }
                }
            }
            for (int num3 = defeatedParty.WoundedInBattle.Count - 1; num3 >= 0; num3--)
            {
                characterObject = defeatedParty.WoundedInBattle.GetCharacterAtIndex(num3);
                for (int num4 = 0; num4 < defeatedParty.WoundedInBattle.GetElementNumber(num3); num4++)
                {
                    MapEventParty mapEventParty2 = MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances(lootCasualtyChances);
                    if (mapEventParty2 != null)
                    {
                        __instance.LootCasualtyCharacter(characterObject, mapEventParty2, defeatedParty, aITradePenalty, flag ? MBRandom.RoundRandomized(f) : int.MinValue, itemRoster);
                    }
                }
            }
            if (flag && itemRoster.Count > 0)
            {
                CampaignEventDispatcher.Instance.OnLootDistributedToParty(PartyBase.MainParty, party, itemRoster);
                playerBattleParty.RosterToReceiveLootItems.Add(itemRoster);
            }
        }

        return false;
    }
}
