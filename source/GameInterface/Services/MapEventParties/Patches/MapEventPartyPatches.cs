using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventParties.Messages;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEventParties.Patches;

[HarmonyPatch(typeof(MapEventParty))]
internal class MapEventPartyPatches
{
    [HarmonyPatch(nameof(MapEventParty.OnTroopKilled))]
    [HarmonyPrefix]
    private static bool PrefixOnTroopKilled(MapEventParty __instance, ref UniqueTroopDescriptor troopSeed)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsServer) return true;

        __instance._roster.OnTroopKilled(troopSeed);
        MessageBroker.Instance.Publish(__instance, new OnTroopKilledAttempted(__instance, troopSeed.UniqueSeed));

        return false;
    }

    [HarmonyPatch(nameof(MapEventParty.OnTroopWounded))]
    [HarmonyPrefix]
    private static bool PrefixOnTroopWounded(MapEventParty __instance, ref UniqueTroopDescriptor troopSeed)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsServer) return true;

        __instance._roster.OnTroopWounded(troopSeed);
        MessageBroker.Instance.Publish(__instance, new OnTroopWoundedAttempted(__instance, troopSeed.UniqueSeed));

        return false;
    }

    [HarmonyPatch(nameof(MapEventParty.OnTroopRouted))]
    [HarmonyPrefix]
    private static bool PrefixOnTroopRouted(MapEventParty __instance, ref UniqueTroopDescriptor troopSeed)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;  

        __instance._roster.OnTroopRouted(troopSeed);
        MessageBroker.Instance.Publish(__instance, new OnTroopRoutedAttempted(__instance, troopSeed.UniqueSeed));

        return false;
    }

    [HarmonyPatch(nameof(MapEventParty.CommitXpGain))]
    [HarmonyPrefix]
    private static bool PrefixCommitXpGain(MapEventParty __instance)
    {
        if (__instance.Party.MobileParty != null)
        {
            int num = 0;
            Hero hero = __instance.Party.LeaderHero ?? __instance.Party.Owner;
            bool flag = hero != null && hero.IsAlive;
            Dictionary<CharacterObject, int> dictionary = new Dictionary<CharacterObject, int>();
            foreach (FlattenedTroopRosterElement troopRosterElement in __instance._roster)
            {
                if (!troopRosterElement.IsKilled && troopRosterElement.XpGained > 0)
                {
                    CharacterObject troop = troopRosterElement.Troop;
                    int num2 = MathF.Round(Campaign.Current.Models.PartyTrainingModel.CalculateXpGainFromBattles(troopRosterElement, __instance.Party).ResultNumber);
                    int num3;
                    if (!dictionary.TryGetValue(troop, out num3))
                    {
                        dictionary.Add(troop, num2);
                    }
                    else
                    {
                        dictionary[troop] = num3 + num2;
                    }
                }
            }
            __instance._roster.ResetTroopXP();
            foreach (KeyValuePair<CharacterObject, int> keyValuePair in dictionary)
            {
                CharacterObject key = keyValuePair.Key;
                int value = keyValuePair.Value;
                int num4;
                MobilePartyHelper.CanTroopGainXp(__instance.Party, key, out num4);
                num4 = Math.Min(num4, value);
                int num5 = value - num4;
                if (num4 > 0)
                {
                    int num6 = Campaign.Current.Models.PartyTrainingModel.GenerateSharedXp(key, num4, __instance.Party.MobileParty);
                    if (num6 > 0)
                    {
                        num += num6;
                        if (num5 > 0)
                        {
                            int num7 = Math.Min(num6, num5);
                            num5 -= num7;
                            num6 -= num7;
                        }
                        num4 -= num6;
                    }
                    if (!key.IsHero)
                    {
                        __instance.Party.MemberRoster.AddXpToTroop(key, num4);
                    }
                }
                if (flag && num5 > 0)
                {
                    SkillLevelingManager.OnBattleEnded(__instance.Party, key, num5);
                }
            }
            if (num > 0)
            {
                MobilePartyHelper.PartyAddSharedXp(__instance.Party.MobileParty, (float)num);
            }
        }

        return false;
    }
}
