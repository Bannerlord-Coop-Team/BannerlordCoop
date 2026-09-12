using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Patch that replaces IsMainParty check to IsPlayerParty,
/// otherwise players could also get invited.
/// </summary>
[HarmonyPatch(typeof(DefaultArmyManagementCalculationModel))]
internal class ArmyManagementCalculationPatches
{
    [HarmonyPatch(nameof(DefaultArmyManagementCalculationModel.CanLordCreateArmy))]
    [HarmonyPrefix]
    private static bool CanLordCreateArmyPrefix(DefaultArmyManagementCalculationModel __instance, MobileParty mobileParty, out MBList<MobileParty> possibleArmyMembers, ref bool __result)
    {
        possibleArmyMembers = new MBList<MobileParty>();
        Kingdom kingdom = mobileParty.MapFaction as Kingdom;
        if (!mobileParty.IsCurrentlyAtSea && mobileParty.LeaderHero.Clan.Influence > 100f && !mobileParty.LeaderHero.Clan.IsUnderMercenaryService && (float)mobileParty.GetNumDaysForFoodToLast() > Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege)
        {
            if (kingdom.FactionsAtWarWith.AnyQ((IFaction x) => x.Fiefs.Any<Town>()) && mobileParty.PartySizeRatio > Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy && (mobileParty.LeaderHero.Clan.Leader == mobileParty.LeaderHero || (mobileParty.LeaderHero.Clan.Leader.PartyBelongedTo == null && mobileParty.LeaderHero.Clan.WarPartyComponents != null && mobileParty.LeaderHero.Clan.WarPartyComponents.FirstOrDefault<WarPartyComponent>() == mobileParty.WarPartyComponent)))
            {
                __instance.GetInfluenceBudgetWhileCreatingArmy(mobileParty);
                List<ValueTuple<MobileParty, float, int>> list = new List<ValueTuple<MobileParty, float, int>>();
                foreach (WarPartyComponent warPartyComponent in mobileParty.MapFaction.WarPartyComponents)
                {
                    MobileParty mobileParty2 = warPartyComponent.MobileParty;
                    Hero leaderHero = mobileParty2.LeaderHero;
                    if (mobileParty2.IsLordParty && mobileParty2.Army == null && mobileParty2 != mobileParty && leaderHero != null && !mobileParty2.IsPlayerParty() && leaderHero != leaderHero.MapFaction.Leader && !mobileParty2.Ai.DoNotMakeNewDecisions)
                    {
                        Settlement currentSettlement = mobileParty2.CurrentSettlement;
                        if (((currentSettlement != null) ? currentSettlement.SiegeEvent : null) == null && !mobileParty2.IsDisbanding && (float)mobileParty2.GetNumDaysForFoodToLast() > Campaign.Current.Models.ArmyManagementCalculationModel.MinimumNeededFoodInDaysToCallToArmy && mobileParty2.PartySizeRatio > Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy && leaderHero.CanLeadParty() && !mobileParty2.IsInRaftState && mobileParty2.MapEvent == null && mobileParty2.BesiegedSettlement == null)
                        {
                            IDisbandPartyCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
                            if (campaignBehavior == null || !campaignBehavior.IsPartyWaitingForDisband(mobileParty2))
                            {
                                float maximumDistanceToCallToArmy = Campaign.Current.Models.ArmyManagementCalculationModel.MaximumDistanceToCallToArmy;
                                float num;
                                if (DistanceHelper.GetDistanceBetweenMobilePartyToMobileParty(mobileParty2, mobileParty, mobileParty2.NavigationCapability, out num) < maximumDistanceToCallToArmy)
                                {
                                    bool flag = false;
                                    using (List<ValueTuple<MobileParty, float, int>>.Enumerator enumerator2 = list.GetEnumerator())
                                    {
                                        while (enumerator2.MoveNext())
                                        {
                                            if (enumerator2.Current.Item1 == mobileParty2)
                                            {
                                                flag = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (!flag)
                                    {
                                        int num2 = Campaign.Current.Models.ArmyManagementCalculationModel.CalculatePartyInfluenceCost(mobileParty, mobileParty2);
                                        float estimatedStrength = mobileParty2.Party.EstimatedStrength;
                                        float num3 = 1f - ((float)mobileParty2.Party.MemberRoster.TotalWounded / (float)mobileParty2.Party.MemberRoster.TotalManCount);
                                        float item = estimatedStrength / ((float)num2 + 0.1f) * num3;
                                        list.Add(new ValueTuple<MobileParty, float, int>(mobileParty2, item, num2));
                                    }
                                }
                            }
                        }
                    }
                }
                list = list.OrderByQ((ValueTuple<MobileParty, float, int> x) => x.Item2).ToListQ<ValueTuple<MobileParty, float, int>>();
                int count = kingdom.WarPartyComponents.Count;
                int num4 = kingdom.Armies.SumQ((Army x) => x.Parties.Count);
                int num5 = MathF.Ceiling(((float)count * 0.7f) - (float)num4);
                if (num5 > 0)
                {
                    if (num5 < list.Count)
                    {
                        list.RemoveRange(num5, list.Count - num5);
                    }
                    possibleArmyMembers = list.SelectQ((ValueTuple<MobileParty, float, int> x) => x.Item1).ToMBList<MobileParty>();
                    if (possibleArmyMembers.AnyQ<MobileParty>())
                    {
                        if (kingdom.Settlements.Count == 0)
                        {
                            __result = true;
                            return false;
                        }
                        float num6 = mobileParty.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                        foreach (MobileParty mobileParty3 in possibleArmyMembers)
                        {
                            num6 += mobileParty3.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                        }
                        if (num6 < 1000f)
                        {
                            possibleArmyMembers.Clear();
                            __result = false;
                            return false;
                        }
                        __result = true;
                        return false;
                    }
                }
            }
        }
        __result = false;
        return false;
    }
}
