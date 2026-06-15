using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class TickPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

        [HarmonyPatch(nameof(CraftingCampaignBehavior.HourlyTick))]
        [HarmonyPrefix]
        public static bool HourlyTickPrefix(ref CraftingCampaignBehavior __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Only let server handle ticks
            if (ModInformation.IsClient) return false;

            // Replace TaleWorlds implementation to allow stamina recovery outside of settlements
            foreach (KeyValuePair<Hero, CraftingCampaignBehavior.HeroCraftingRecord> keyValuePair in __instance._heroCraftingRecords)
            {
                int maxHeroCraftingStamina = __instance.GetMaxHeroCraftingStamina(keyValuePair.Key);
                if (keyValuePair.Value.CraftingStamina < maxHeroCraftingStamina)
                {
                    keyValuePair.Value.CraftingStamina = MathF.Min(maxHeroCraftingStamina, keyValuePair.Value.CraftingStamina + CraftingCampaignBehavior.GetStaminaHourlyRecoveryRate(keyValuePair.Key));
                }
            }

            // Update on all clients with message including up to date crafting records
            var message = new HourTicked(__instance);
            MessageBroker.Instance.Publish(__instance, message);

            return false;
        }

        [HarmonyPatch(nameof(CraftingCampaignBehavior.GetStaminaHourlyRecoveryRate))]
        [HarmonyPrefix]
        public static bool GetStaminaHourlyRecoveryRatePrefix(ref int __result, Hero hero)
        {
            int num = 5 + MathF.Round((float)hero.GetSkillValue(DefaultSkills.Crafting) * 0.025f);
            if (hero.GetPerkValue(DefaultPerks.Athletics.Stamina))
            {
                num += MathF.Round((float)num * DefaultPerks.Athletics.Stamina.PrimaryBonus);
            }

            // Multiply the vanilla result to be 10 times slower as stamina now regenerates outside of settlements
            // Later expand this to be part of a config for players if its too slow or fast
            // Round up the result so that crafting stamina always regenerates partially
            __result = MathF.Ceiling(num * 0.1f);
            return false;
        }

        [HarmonyPatch(nameof(CraftingCampaignBehavior.DailyTickSettlement))]
        [HarmonyPrefix]
        public static bool DailyTickSettlement(ref CraftingCampaignBehavior __instance, Settlement settlement)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Only let server handle ticks
            if (ModInformation.IsClient) return false;

            // Replace vanilla functionality
            var message = new DailySettlementTick(__instance, settlement);
            MessageBroker.Instance.Publish(__instance, message);

            return false;
        }

        [HarmonyPatch(nameof(CraftingCampaignBehavior.DailyTick))]
        [HarmonyPrefix]
        public static bool DailyTick(ref CraftingCampaignBehavior __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Only let server handle ticks
            if (ModInformation.IsClient) return false;

            // Run on server
            return true;
        }
    }
}
