using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>
    /// Disables functionality of policies in game.
    /// </summary>
    /// <seealso cref="PolicyObject"/>
    [HarmonyPatch(typeof(Kingdom))]
    internal class KingdomPatches
    {
        [HarmonyPatch("AddDecision")]
        [HarmonyPrefix]
        public static bool AddDecisionPrefix()
        {
            return false;
        }

        [HarmonyPatch("RemoveDecision")]
        [HarmonyPrefix]
        public static bool RemoveDecisionPrefix()
        {
            return false;
        }

        [HarmonyPatch("AddPolicy")]
        [HarmonyPrefix]
        public static bool AddPolicyPrefix()
        {
            return false;
        }

        [HarmonyPatch("RemovePolicy")]
        [HarmonyPrefix]
        public static bool RemovePolicyPrefix()
        {
            return false;
        }


        [HarmonyPatch(nameof(Kingdom.CreateArmy), MethodType.Normal)]
        [HarmonyPrefix]
        public static bool CreateArmyPrefix(ref Kingdom __instance, ref Hero armyLeader, ref Settlement targetSettlement, Army.ArmyTypes selectedArmyType)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;
            var message = new ArmyInKingdomCreated(__instance.StringId, armyLeader.StringId, targetSettlement.StringId, selectedArmyType.ToString());
            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }

        public static void CreateArmyInKingdom(Kingdom kingdom, Hero armyLeader, Settlement targetSettlement, Army.ArmyTypes selectedArmyType)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    kingdom.CreateArmy(armyLeader, targetSettlement, selectedArmyType);
                }
            });
        }
    }
}
