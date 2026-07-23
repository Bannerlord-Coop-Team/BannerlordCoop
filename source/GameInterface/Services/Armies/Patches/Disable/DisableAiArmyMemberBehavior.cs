using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Helpers;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace GameInterface.Services.Armies.Patches.Disable;

[HarmonyPatch(typeof(AiArmyMemberBehavior))]
internal class DisableAiArmyMemberBehavior
{
    [HarmonyPatch(nameof(AiArmyMemberBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    /// <summary>
    /// Patch for replacing IsMainParty to IsPlayerParty
    /// <summary>
    [HarmonyPatch(nameof(AiArmyMemberBehavior.AiHourlyTick))]
    [HarmonyPrefix]
    public static bool AiHourlyTickPrefix(AiArmyMemberBehavior __instance, MobileParty mobileParty, PartyThinkParams p)
    {
        if (mobileParty.Army == null || mobileParty.Army.LeaderParty == mobileParty)
        {
            return false;
        }

        if (mobileParty.AttachedTo == null)
        {
            if (mobileParty.Army.LeaderParty.CurrentSettlement != null && mobileParty.Army.LeaderParty.CurrentSettlement.IsUnderSiege && (mobileParty.Army.LeaderParty.CurrentSettlement.SiegeEvent.IsBlockadeActive || !mobileParty.HasNavalNavigationCapability))
            {
                return false;
            }
            if (mobileParty.CurrentSettlement != null && mobileParty.CurrentSettlement.IsUnderSiege)
            {
                return false;
            }
        }

        MobileParty.NavigationType navigationType = MobileParty.NavigationType.None;
        float num = float.MaxValue;
        bool isTargetingPort = false;
        bool isFromPort = false;

        if (mobileParty.Army.LeaderParty.CurrentSettlement != null)
        {
            SiegeEvent siegeEvent = mobileParty.Army.LeaderParty.CurrentSettlement.SiegeEvent;
            bool flag = siegeEvent == null;
            bool flag2 = mobileParty.HasNavalNavigationCapability && mobileParty.Army.LeaderParty.CurrentSettlement.HasPort && (siegeEvent == null || (!siegeEvent.IsBlockadeActive && mobileParty.HasNavalNavigationCapability));

            if (flag)
            {
                AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(mobileParty, mobileParty.Army.LeaderParty.CurrentSettlement, false, out navigationType, out num, out isFromPort);
            }

            if (flag2)
            {
                MobileParty.NavigationType navigationType2;
                float num2;
                bool flag3;
                AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(mobileParty, mobileParty.Army.LeaderParty.CurrentSettlement, true, out navigationType2, out num2, out flag3);
                if (num2 < num)
                {
                    navigationType = navigationType2;
                    num = num2;
                    isFromPort = flag3;
                    isTargetingPort = true;
                }
            }
        }
        else
        {
            AiHelper.GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(mobileParty, mobileParty.Army.LeaderParty, out navigationType, out num);
        }

        ValueTuple<AIBehaviorData, float> valueTuple;

        if (navigationType != MobileParty.NavigationType.None)
        {
            float num3 = __instance.FollowingArmyLeaderMaxScore;
            float num4 = 1f;
            float num5 = mobileParty.Army.LeaderParty.IsPlayerParty()
                ? Campaign.Current.Models.ArmyManagementCalculationModel.PlayerMobilePartySizeRatioToCallToArmy
                : Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy;

            int daysFoodLeft = mobileParty.GetNumDaysForFoodToLast();
            float minFoodDays = Campaign.Current.Models.ArmyManagementCalculationModel.MinimumNeededFoodInDaysToCallToArmy;

            if ((float)daysFoodLeft < minFoodDays || mobileParty.PartySizeRatio < num5)
            {
                num3 = __instance.FollowingArmyLeaderMinScore;
                float num6 = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigationType) * 0.5f;
                if (num6 > num)
                {
                    num4 = MathF.Clamp(num6 / (num + 0.1f), 1f, __instance.FollowingArmyLeaderMaxScore / __instance.FollowingArmyLeaderMinScore);
                }
            }
            AIBehaviorData item = new AIBehaviorData(mobileParty.Army.LeaderParty, AiBehavior.EscortParty, navigationType, false, isFromPort, isTargetingPort);
            float item2 = MathF.Clamp(num3 * num4, __instance.FollowingArmyLeaderMinScore, __instance.FollowingArmyLeaderMaxScore);
            valueTuple = new ValueTuple<AIBehaviorData, float>(item, item2);

            p.AddBehaviorScore(valueTuple);
            return false;
        }

        AIBehaviorData item3 = new AIBehaviorData(mobileParty.Army.LeaderParty, AiBehavior.EscortParty, mobileParty.NavigationCapability, false, isFromPort, false);
        float armyLeaderIsUnreachableScore = __instance.ArmyLeaderIsUnreachableScore;
        valueTuple = new ValueTuple<AIBehaviorData, float>(item3, armyLeaderIsUnreachableScore);

        p.AddBehaviorScore(valueTuple);
        return false;
    }
}