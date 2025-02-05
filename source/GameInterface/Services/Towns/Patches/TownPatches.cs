using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Towns.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Patches
{
    /// <summary>
    /// Disables all functionality for Town
    /// </summary>
    [HarmonyPatch(typeof(Town))]
    public class TownPatches
    {
        

        [HarmonyPatch(nameof(Town.Governor), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool TownGovernorPrefix(ref Town __instance, ref Hero value)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient) return false;
            if (__instance.Governor == value) return false;

            
            var message = new TownGovernorChanged(__instance.StringId, value?.StringId);
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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient) return false;

            var message = new TownSoldItemsChanged(__instance.StringId, logList);
            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        public static void ChangeSetSoldItems(Town town, IEnumerable<Town.SellLog> logList)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    town.SetSoldItems(logList);
                }
            });
        }
    }

    [HarmonyPatch(typeof(ClanVariablesCampaignBehavior))]
    internal class UpdateClanSettlementAutoRecruitmentPatches
    {
        private static FieldInfo GarrisonAutoRecruitmentIsEnabled => typeof(Town).GetField(nameof(Town.GarrisonAutoRecruitmentIsEnabled));
        private static MethodInfo PublishTownGarrisonAutoRecruitmentIsEnabledChangedMethod => typeof(UpdateClanSettlementAutoRecruitmentPatches).GetMethod("PublishTownGarrisonAutoRecruitmentIsEnabledChanged", BindingFlags.Static | BindingFlags.NonPublic);
        
        [HarmonyPatch("UpdateClanSettlementAutoRecruitment")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateClanSettlementAutoRecruitment(IEnumerable<CodeInstruction> instructions)
        {
            foreach(var instruction in instructions)
            {
                // Replaces stfld     bool TaleWorlds.CampaignSystem.Settlements.Town::GarrisonAutoRecruitmentIsEnabled
                // With calling PublishTownInRebelliousStateChanged
                if (instruction.opcode == OpCodes.Stfld && 
                    instruction.operand as FieldInfo == GarrisonAutoRecruitmentIsEnabled)
                {
                    yield return new CodeInstruction(OpCodes.Call, PublishTownGarrisonAutoRecruitmentIsEnabledChangedMethod);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        internal static void PublishTownGarrisonAutoRecruitmentIsEnabledChanged(Town town, bool garrisonAutoRecruitmentIsEnabled)
        {
            // Allow setting if original call exists
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                town.GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
                return;
            }

            if (ModInformation.IsClient) return;
            if (town.GarrisonAutoRecruitmentIsEnabled == garrisonAutoRecruitmentIsEnabled) return;

            town.GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
            var message = new TownGarrisonAutoRecruitmentIsEnabledChanged(town.StringId, garrisonAutoRecruitmentIsEnabled);
            MessageBroker.Instance.Publish(town, message);
        }
    }

    [HarmonyPatch(typeof(RebellionsCampaignBehavior))]
    internal class RebellionsCampaignBehaviorPatches
    {
        private static FieldInfo InRebelliousState => typeof(Town).GetField(nameof(Town.InRebelliousState));
        private static MethodInfo PublishTownInRebelliousStateChangedMethod => typeof(RebellionsCampaignBehaviorPatches).GetMethod("PublishTownInRebelliousStateChanged", BindingFlags.Static | BindingFlags.NonPublic);

        [HarmonyPatch("CheckAndSetTownRebelliousState")]
        [HarmonyPatch("ApplyRebellionConsequencesToSettlement")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ApplyRebellionConsequencesToSettlementPostfix(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                // Replaces stfld     bool TaleWorlds.CampaignSystem.Settlements.Town::InRebelliousState
                // With calling PublishTownInRebelliousStateChanged
                if (instruction.opcode == OpCodes.Stfld &&
                    instruction.operand as FieldInfo == InRebelliousState)
                {
                    yield return new CodeInstruction(OpCodes.Call, PublishTownInRebelliousStateChangedMethod);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        internal static void PublishTownInRebelliousStateChanged(Town town, bool rebelliousState)
        {
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                town.InRebelliousState = rebelliousState;
                return;
            }

            if (ModInformation.IsClient) return;
            if (town.InRebelliousState == rebelliousState) return;

            town.InRebelliousState = rebelliousState;
            var message = new TownInRebelliousStateChanged(town.StringId, rebelliousState);
            MessageBroker.Instance.Publish(town, message);
        }
    }
}
