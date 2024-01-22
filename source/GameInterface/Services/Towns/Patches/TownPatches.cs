using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Towns.Messages;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Patches
{
    /// <summary>
    /// Disables all functionality for Town
    /// </summary>
    [HarmonyPatch(typeof(Town))]
    public class TownPatches
    {
        [HarmonyPatch("DailyTick")]
        [HarmonyPrefix]
        private static bool DailyTickPrefix()
        {
            return ModInformation.IsServer;
        }

        [HarmonyPatch(nameof(Town.Governor), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool TownGovernorPrefix(ref Town __instance, ref Hero value)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;
            if (__instance.Governor == value) return false;

            var message = new TownGovernorChanged(__instance.StringId, value.StringId);
            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        public static void ChangeTownGovernor(Town town, Hero governor)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    town.Governor = governor;
                }
            });

        }

        [HarmonyPatch(nameof(Town.Prosperity), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool TownProsperityPrefix(ref Town __instance, ref float value)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;
            if (__instance.Prosperity == value) return false;

            var message = new TownProsperityChanged(__instance.StringId, value);
            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        public static void ChangeTownProsperity(Town town, float prosperity)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    town.Prosperity = prosperity;
                }
            });

        }

        [HarmonyPatch(nameof(Town.Loyalty), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool TownLoyaltyPrefix(ref Town __instance, ref float value)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;
            if (__instance.Loyalty == value) return false;

            var message = new TownLoyaltyChanged(__instance.StringId, value);
            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        public static void ChangeTownLoyalty(Town town, float loyalty)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    town.Loyalty = loyalty;
                }
            });

        }

        [HarmonyPatch(nameof(Town.Security), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool TownSecurityPrefix(ref Town __instance, ref float value)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;
            if (__instance.Security == value) return false;

            var message = new TownSecurityChanged(__instance.StringId, value);
            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        public static void ChangeTownSecurity(Town town, float security)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    town.Security = security;
                }
            });

        }

        [HarmonyPatch(nameof(Town.LastCapturedBy), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool TownLastCapturedByPrefix(ref Town __instance, ref Clan value)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;
            if (__instance.LastCapturedBy == value) return false;

            var message = new TownLastCapturedByChanged(__instance.StringId, value.StringId);
            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        public static void ChangeTownLastCapturedBy(Town town, Clan lastCapturedBy)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    town.LastCapturedBy = lastCapturedBy;
                }
            });

        }

        public static void ChangeTownInRebelliousState(Town town, bool inRebelliousState)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                town.InRebelliousState = inRebelliousState;
            });
        }

        public static void ChangeTownGarrisonAutoRecruitmentIsEnabled(Town town, bool garrisonAutoRecruitmentIsEnabled)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                town.GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
            });
        }

        [HarmonyPatch(nameof(Town.TradeTaxAccumulated), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool TownTradeTaxAccumulatedPrefix(ref Town __instance, ref int value)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;
            if (__instance.TradeTaxAccumulated == value) return false;

            var message = new TownTradeTaxAccumulatedChanged(__instance.StringId, value);
            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        public static void ChangeTradeTaxAccumulated(Town town, int tradeTaxAccumulated)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    town.TradeTaxAccumulated = tradeTaxAccumulated;
                }
            });
        }

        [HarmonyPatch(nameof(Town.SetSoldItems), MethodType.Normal)]
        [HarmonyPrefix]
        private static bool SetSoldItemsPrefix(Town __instance, IEnumerable<Town.SellLog> logList)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;

            var message = new TownSoldItemsChanged(__instance.StringId, logList);
            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }
    }

    [HarmonyPatch(typeof(ClanVariablesCampaignBehavior))]
    internal class ClanVariablesCampaignBehaviorPatches
    {
        [HarmonyPatch("UpdateClanSettlementAutoRecruitment")]
        [HarmonyPrefix]
        private static bool UpdateClanSettlementAutoRecruitment(Clan clan)
        {
            if (clan.MapFaction != null && clan.MapFaction.IsKingdomFaction)
            {
                foreach (Settlement settlement in clan.Settlements)
                {
                    if (settlement.IsFortification && settlement.Town.GarrisonParty != null && !settlement.Town.GarrisonAutoRecruitmentIsEnabled)
                    {
                        Town town = settlement.Town.GarrisonParty.CurrentSettlement.Town;
                        town.GarrisonAutoRecruitmentIsEnabled = true;
                        var message = new TownGarrisonAutoRecruitmentIsEnabledChanged(town.StringId, town.GarrisonAutoRecruitmentIsEnabled);
                        MessageBroker.Instance.Publish(town, message);
                    }
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(RebellionsCampaignBehavior))]
    internal class RebellionsCampaignBehaviorPatches
    {
        [HarmonyPatch("ApplyRebellionConsequencesToSettlement")]
        [HarmonyPostfix]
        private static void ApplyRebellionConsequencesToSettlementPostfix(Settlement settlement)
        {
            Town town = settlement.Town;
            var message = new TownInRebelliousStateChanged(town.StringId, town.InRebelliousState);
            MessageBroker.Instance.Publish(town, message);
        }
    }


    [HarmonyPatch(typeof(CampaignEvents))]
    internal class CampaignEventsPatches
    {
        [HarmonyPatch("TownRebelliousStateChanged")]
        [HarmonyPostfix]
        private static void TownRebelliousStateChangedPostfix(Town town, bool rebelliousState)
        {
            var message = new TownInRebelliousStateChanged(town.StringId, rebelliousState);
            MessageBroker.Instance.Publish(town, message);
        }
    }


    [HarmonyPatch(typeof(DefaultSettlementProsperityModel))]
    internal class DefaultSettlementProsperityModelPatches
    {
        [HarmonyPatch(nameof(DefaultSettlementProsperityModel.CalculateProsperityChange))]
        [HarmonyPrefix]
        private static bool CalculateProsperityChangePatch()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(BuildingHelper))]
    internal class BuilderHelperPatches
    {
        [HarmonyPatch("AddDefaultDailyBonus")]
        [HarmonyPrefix]
        private static bool AddDefaultDailyBonusPatch()
        {
            return false;
        }
    }
}
