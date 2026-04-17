using Common;
using Common.Logging;
using HarmonyLib;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using static TaleWorlds.CampaignSystem.Party.MobilePartyAi;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(Campaign))]
internal class PartiesThinkPatch
{
    // TODO move to config
    private const int UPDATES_PER_TICK = 100;
    private const int TICK_DELAY_MS = 100;

    private static Task delay = Task.CompletedTask;

    private static int CurrentStartIdx = 0;

    private static readonly ILogger Logger = LogManager.GetLogger<PartiesThinkPatch>();

    [HarmonyPatch("PartiesThink")]
    [HarmonyPrefix]
    private static bool PartiesThinkPrefix(Campaign __instance, ref float dt)
    {
        if (ModInformation.IsClient) return false;

        if (delay.IsCompleted == false) return false;

        delay = Task.Delay(TICK_DELAY_MS);

        for (int i = 0; i < UPDATES_PER_TICK; i++)
        {
            var currentIdx = (CurrentStartIdx + i) % __instance.MobileParties.Count;

            var ai = __instance.MobileParties[currentIdx]?.Ai;

            Tick(ai, dt);
        }

        CurrentStartIdx = (CurrentStartIdx + UPDATES_PER_TICK) % __instance.MobileParties.Count;

        return false;
    }

    private static void Tick(MobilePartyAi ai, float dt)
    {
        if (ai.DefaultBehaviorNeedsUpdate)
        {
            ai._nextAiCheckTime = CampaignTime.Now;
            ai.DefaultBehaviorNeedsUpdate = false;
        }

        if (!ai._nextAiCheckTime.IsFuture)
        {
            TickInternal(ai);
            float num = Campaign.Current.Models.MobilePartyAIModel.AiCheckInterval * (0.6f + 0.1f * MBRandom.RandomFloat);
            num *= ((ai._mobileParty.ShortTermTargetParty == MobileParty.MainParty && ai._mobileParty.ShortTermBehavior == AiBehavior.EngageParty) ? 0.5f : 1f);
            if (ai._mobileParty.IsFleeing())
            {
                num *= ((ai._mobileParty.ShortTermTargetParty == MobileParty.MainParty) ? 0.5f : 0.75f);
                num *= (ai._mobileParty.IsBandit ? 10f : 1f);
                num *= ((!ai._mobileParty.IsBandit && ai._fleeingData.CcwFleeDirectionIsBlocked && ai._fleeingData.CwFleeDirectionIsBlocked) ? 15f : 1f);
            }

            num *= ((ai._mobileParty.SiegeEvent != null) ? 2f : 1f);
            long valueInSeconds = (long)(num * (float)CampaignTime.MinutesInHour * (float)CampaignTime.SecondsInMinute);
            ai._nextAiCheckTime = CampaignTime.Now + CampaignTime.Seconds(valueInSeconds);
        }
    }

    private static void TickInternal(MobilePartyAi ai)
    {
        if (ai._mobileParty.MapEvent != null || !ai._mobileParty.IsActive)
        {
            return;
        }

        if (ai._mobileParty == MobileParty.MainParty && MobileParty.MainParty.DefaultBehavior == AiBehavior.EngageParty && !MobileParty.MainParty.TargetParty.IsVisible)
        {
            MobileParty.MainParty.SetMoveModeHold();
        }

        if (ai.IsDisabled)
        {
            if (ai.EnableAgainAtHourIsPast())
            {
                ai.EnableAi();
            }
        }
        else if (ai._mobileParty.Army == null || !ai._mobileParty.Army.LeaderParty.AttachedParties.Contains(ai._mobileParty))
        {
            GetBehaviors(ai, out var bestAiBehavior, out var behaviorObject, out var bestTargetPoint);
            ai.SetAiBehavior(bestAiBehavior, behaviorObject, bestTargetPoint);
        }
    }

    private static void GetBehaviors(MobilePartyAi ai, out AiBehavior bestAiBehavior, out IInteractablePoint behaviorObject, out CampaignVec2 bestTargetPoint)
    {
        bestAiBehavior = ai._mobileParty.DefaultBehavior;
        MobileParty mobileParty = ai._mobileParty.TargetParty ?? ai._mobileParty.ShortTermTargetParty;
        bestTargetPoint = ai._mobileParty.TargetPosition;
        Vec2 averageEnemyVec = new Vec2(0f, 0f);
        if (Campaign.Current.GameStarted && Campaign.Current.Models.MobilePartyAIModel.ShouldPartyCheckInitiativeBehavior(ai._mobileParty))
        {
            GetBestInitiativeBehavior(Campaign.Current.Models.MobilePartyAIModel, ai._mobileParty, out var bestInitiativeBehavior, out var bestInitiativeTargetParty, out var bestInitiativeBehaviorScore, out averageEnemyVec);
            if (!ai.DoNotMakeNewDecisions || (bestInitiativeTargetParty != null && ai._mobileParty.TargetSettlement != null && ((bestInitiativeTargetParty.MapEvent != null && bestInitiativeTargetParty.MapEvent.MapEventSettlement == ai._mobileParty.TargetSettlement) || bestInitiativeTargetParty.BesiegedSettlement == ai._mobileParty.TargetSettlement)))
            {
                if (bestInitiativeBehaviorScore > 1f && (ai._mobileParty.DefaultBehavior != AiBehavior.DefendSettlement || bestInitiativeTargetParty != ai._mobileParty.TargetSettlement.LastAttackerParty))
                {
                    bestAiBehavior = bestInitiativeBehavior;
                    mobileParty = bestInitiativeTargetParty;
                    if (bestInitiativeBehavior == AiBehavior.EngageParty && bestInitiativeTargetParty.IsCurrentlyAtSea != ai._mobileParty.IsCurrentlyAtSea && !ai._mobileParty.IsTransitionInProgress)
                    {
                        ai._mobileParty.StartTransitionNextFrameToExitFromPort = true;
                    }
                }
                else if (MobileParty.IsFleeBehavior(bestInitiativeBehavior) && MobileParty.IsFleeBehavior(ai._mobileParty.ShortTermBehavior))
                {
                    float num = ai._mobileParty.AiBehaviorTarget.DistanceSquared(ai._mobileParty.Position);
                    float lastCalculatedSpeed = ai._mobileParty._lastCalculatedSpeed;
                    if (num >= lastCalculatedSpeed * lastCalculatedSpeed * Campaign.Current.Models.MobilePartyAIModel.AiCheckInterval * Campaign.Current.Models.MobilePartyAIModel.AiCheckInterval)
                    {
                        bestAiBehavior = ai._mobileParty.ShortTermBehavior;
                        mobileParty = ((ai._mobileParty.ShortTermBehavior != AiBehavior.FleeToGate) ? (ai._mobileParty.ShortTermTargetParty ?? bestInitiativeTargetParty) : (ai._mobileParty.TargetParty ?? bestInitiativeTargetParty));
                    }
                }

                if (MobileParty.IsFleeBehavior(ai._mobileParty.ShortTermBehavior) && !MobileParty.IsFleeBehavior(bestAiBehavior))
                {
                    ai._fleeingData.Clear();
                }

                if (bestInitiativeBehavior == AiBehavior.DefendSettlement && bestInitiativeTargetParty.DefaultBehavior == AiBehavior.DefendSettlement && bestInitiativeTargetParty.MapEvent != null)
                {
                    bestAiBehavior = AiBehavior.EngageParty;
                    mobileParty = bestInitiativeTargetParty.ShortTermTargetParty;
                }
            }
        }

        ai.IsAlerted = false;
        AiBehavior shortTermBehavior = bestAiBehavior;
        CampaignVec2 shortTermTargetPoint = bestTargetPoint;
        Settlement shortTermTargetSettlement = ai._mobileParty.TargetSettlement;
        MobileParty shortTermTargetParty = mobileParty;
        switch (bestAiBehavior)
        {
            case AiBehavior.GoAroundParty:
                shortTermTargetParty = ai._mobileParty.TargetParty;
                ai.GetGoAroundPartyBehavior(ai._mobileParty.TargetParty, out shortTermBehavior, out shortTermTargetPoint);
                break;
            case AiBehavior.EngageParty:
                shortTermTargetPoint = shortTermTargetParty.Position;
                if (shortTermTargetParty.SiegeEvent != null && ai._mobileParty.IsCurrentlyAtSea && ai._mobileParty.IsTargetingPort)
                {
                    shortTermTargetPoint = shortTermTargetSettlement.PortPosition;
                    shortTermBehavior = AiBehavior.GoToSettlement;
                    ai._mobileParty.SetShortTermBehavior(AiBehavior.GoToSettlement, shortTermTargetParty.SiegeEvent.BesiegedSettlement.Party);
                }
                else
                {
                    ai._mobileParty.SetShortTermBehavior(AiBehavior.EngageParty, shortTermTargetParty.Party);
                }

                break;
            case AiBehavior.PatrolAroundPoint:
                {
                    bool forceUpdate = ai._mobileParty.ShortTermBehavior == AiBehavior.FleeToPoint || (!ai._mobileParty.IsTransitionInProgress && shortTermTargetPoint.IsOnLand == ai._mobileParty.IsCurrentlyAtSea) || ai._mobileParty.TargetPosition == ai._mobileParty.MoveTargetPoint;
                    if (shortTermTargetPoint.IsOnLand)
                    {
                        ai.GetLandPatrolBehavior(out shortTermBehavior, out shortTermTargetPoint, ai._mobileParty.TargetPosition, forceUpdate);
                    }
                    else
                    {
                        ai.GetNavalPatrolBehavior(out shortTermBehavior, out shortTermTargetPoint, ai._mobileParty.TargetPosition, forceUpdate);
                    }

                    shortTermTargetParty = null;
                    ai._mobileParty.SetShortTermBehavior(AiBehavior.GoToPoint, null);
                    break;
                }
            case AiBehavior.FleeToPoint:
            case AiBehavior.FleeToGate:
            case AiBehavior.FleeToParty:
                ai.IsAlerted = true;
                ai.GetFleeBehavior(out shortTermBehavior, out shortTermTargetPoint, ref shortTermTargetSettlement, mobileParty, averageEnemyVec);
                break;
            case AiBehavior.EscortParty:
                ai.GetFollowBehavior(ref shortTermBehavior, ref shortTermTargetSettlement, mobileParty, out shortTermTargetPoint);
                break;
            case AiBehavior.BesiegeSettlement:
                if (!ai._mobileParty.IsMainParty)
                {
                    ai.GetBesiegeBehavior(out shortTermBehavior, out shortTermTargetPoint, out shortTermTargetSettlement);
                }

                break;
            case AiBehavior.GoToSettlement:
                if (ai._mobileParty.CurrentSettlement == ai._mobileParty.TargetSettlement)
                {
                    ai.GetInSettlementBehavior(ref shortTermBehavior, ref shortTermTargetParty);
                    shortTermTargetPoint = shortTermTargetParty?.Position ?? ai._mobileParty.Position;
                }

                break;
            case AiBehavior.DefendSettlement:
                {
                    Settlement targetSettlement = ai._mobileParty.TargetSettlement;
                    if (targetSettlement == null)
                    {
                        targetSettlement = mobileParty.TargetSettlement;
                    }

                    if (targetSettlement.LastAttackerParty != null && targetSettlement.LastAttackerParty.IsActive)
                    {
                        ai.GetDefendSettlementBehavior(targetSettlement, out shortTermBehavior, out shortTermTargetPoint, out shortTermTargetParty);
                        break;
                    }

                    shortTermBehavior = AiBehavior.GoToPoint;
                    shortTermTargetPoint = ((ai._mobileParty.IsCurrentlyAtSea && targetSettlement.HasPort) ? targetSettlement.PortPosition : targetSettlement.GatePosition);
                    break;
                }
            case AiBehavior.MoveToNearestLandOrPort:
                ai.GetBestMoveToNearestLandBehavior(out shortTermBehavior, out shortTermTargetPoint, out shortTermTargetSettlement);
                break;
        }

        bestAiBehavior = shortTermBehavior;
        bestTargetPoint = shortTermTargetPoint;
        _ = bestTargetPoint.Face;
        if (shortTermTargetParty != null)
        {
            mobileParty = shortTermTargetParty;
        }

        if (bestAiBehavior == AiBehavior.GoToSettlement || bestAiBehavior == AiBehavior.RaidSettlement || bestAiBehavior == AiBehavior.AssaultSettlement || bestAiBehavior == AiBehavior.BesiegeSettlement || (bestAiBehavior == AiBehavior.DefendSettlement && mobileParty == null))
        {
            // Added null check for target settlement
            behaviorObject = ((shortTermTargetSettlement != null) ? shortTermTargetSettlement.Party : ai._mobileParty.TargetSettlement?.Party);
        }
        else if (bestAiBehavior == AiBehavior.EngageParty || bestAiBehavior == AiBehavior.FleeToParty || bestAiBehavior == AiBehavior.GoAroundParty || bestAiBehavior == AiBehavior.EscortParty || bestAiBehavior == AiBehavior.JoinParty || bestAiBehavior == AiBehavior.FleeToPoint || (bestAiBehavior == AiBehavior.DefendSettlement && mobileParty != null))
        {
            behaviorObject = mobileParty?.Party;
        }
        else if (bestAiBehavior == AiBehavior.FleeToGate)
        {
            behaviorObject = shortTermTargetSettlement?.Party ?? ai._mobileParty.ShortTermTargetSettlement.Party;
        }
        else if (bestAiBehavior == AiBehavior.MoveToNearestLandOrPort)
        {
            behaviorObject = ai._mobileParty.Ai.AiBehaviorInteractable;
        }
        else if (bestAiBehavior == AiBehavior.Hold || bestAiBehavior == AiBehavior.None)
        {
            behaviorObject = null;
        }
        else if (bestAiBehavior == AiBehavior.GoToPoint)
        {
            IInteractablePoint interactablePoint;
            if (shortTermTargetParty == null)
            {
                interactablePoint = ai.AiBehaviorInteractable;
            }
            else
            {
                IInteractablePoint party = shortTermTargetParty.Party;
                interactablePoint = party;
            }

            behaviorObject = interactablePoint;
        }
        else if (bestAiBehavior == AiBehavior.DoOperation)
        {
            Debug.FailedAssert("DoOperation", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\MobilePartyAi.cs", "GetBehaviors", 643);
            behaviorObject = null;
        }
        else
        {
            behaviorObject = ai.AiBehaviorInteractable;
        }
    }

    public static void GetBestInitiativeBehavior(MobilePartyAIModel model, MobileParty mobileParty, out AiBehavior bestInitiativeBehavior, out MobileParty bestInitiativeTargetParty, out float bestInitiativeBehaviorScore, out Vec2 averageEnemyVec)
    {
        MobilePartyAi.DangerousPartiesAndTheirVecs.Clear();
        bestInitiativeBehaviorScore = 0f;
        bestInitiativeTargetParty = null;
        bestInitiativeBehavior = AiBehavior.None;
        averageEnemyVec = Vec2.Zero;
        float getEncounterJoiningRadius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
        float num = 2f;
        if (mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint && !mobileParty.AiBehaviorTarget.IsOnLand && mobileParty.IsCurrentlyAtSea)
        {
            num *= 2f;
        }
        float radius = getEncounterJoiningRadius * (num + 1f);
        CampaignVec2 position = mobileParty.Position;
        if (mobileParty.CurrentSettlement != null)
        {
            position = mobileParty.CurrentSettlement.Position;
        }
        LocatableSearchData<MobileParty> locatableSearchData = MobileParty.StartFindingLocatablesAroundPosition(position.ToVec2(), radius);
        MobileParty mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
        while (mobileParty2 != null)
        {
            if (mobileParty2.MapEvent != null && MobileParty.MainParty.MapEvent == mobileParty2.MapEvent && (MobileParty.MainParty.Army == null || MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty) && mobileParty2 != MobileParty.MainParty)
            {
                mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
            }
            else
            {
                if (!mobileParty2.IsGarrison)
                {
                    if ((mobileParty.CurrentSettlement == null || !mobileParty.CurrentSettlement.HasPort) && mobileParty.IsCurrentlyAtSea != mobileParty2.IsCurrentlyAtSea)
                    {
                        mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                        continue;
                    }
                    if ((mobileParty2.IsCurrentlyAtSea && !mobileParty.HasNavalNavigationCapability) || (!mobileParty2.IsCurrentlyAtSea && !mobileParty.HasLandNavigationCapability))
                    {
                        mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                        continue;
                    }
                }
                if (mobileParty.IsLordParty && mobileParty2.IsBandit && mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint && (mobileParty.TargetPosition.IsOnLand == mobileParty.IsCurrentlyAtSea || mobileParty.IsTransitionInProgress))
                {
                    mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                }
                else
                {
                    if (mobileParty2 != mobileParty && mobileParty2.IsActive && IsEnemy(mobileParty2.Party, mobileParty) && !mobileParty2.ShouldBeIgnored && (mobileParty2.CurrentSettlement == null || mobileParty2.IsGarrison || mobileParty2.IsLordParty))
                    {
                        Settlement currentSettlement = mobileParty.CurrentSettlement;
                        if (((currentSettlement != null) ? currentSettlement.SiegeEvent : null) == null && (!mobileParty2.IsGarrison || mobileParty.IsBandit) && (mobileParty2.BesiegerCamp == null || mobileParty2.BesiegerCamp.LeaderParty == mobileParty2) && (mobileParty2.Army == null || mobileParty2.Army.LeaderParty == mobileParty2 || mobileParty2.AttachedTo == null) && (mobileParty2.MapEvent == null || mobileParty2 == MobileParty.MainParty || mobileParty2.Party.MapEvent.MapEventSettlement != null || mobileParty2.Party == mobileParty2.Party.MapEvent.GetLeaderParty(BattleSideEnum.Attacker) || mobileParty2.Party == mobileParty2.Party.MapEvent.GetLeaderParty(BattleSideEnum.Defender)) && (mobileParty2.MapEvent == null || IsEnemy(mobileParty2.MapEvent.AttackerSide.LeaderParty, mobileParty) != IsEnemy(mobileParty2.MapEvent.DefenderSide.LeaderParty, mobileParty)) && (mobileParty2.CurrentSettlement == null || !mobileParty2.CurrentSettlement.IsHideout || !mobileParty.IsBandit))
                        {
                            if (mobileParty.Army != null && mobileParty.AttachedTo == null && mobileParty.Army.LeaderParty != mobileParty && mobileParty2.MapEvent != null && mobileParty2.MapEventSide.OtherSide.LeaderParty.IsMobile && mobileParty2.MapEventSide.OtherSide.LeaderParty.MobileParty.Army != null && mobileParty2.MapEventSide.OtherSide.LeaderParty.MobileParty.Army == mobileParty.Army)
                            {
                                mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                                continue;
                            }
                            CampaignVec2 v;
                            if (mobileParty.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty.IsCurrentlyAtSea && mobileParty.IsTargetingPort)
                            {
                                v = mobileParty.TargetSettlement.PortPosition;
                                num = Campaign.Current.Models.MobilePartyAIModel.SettlementDefendingNearbyPartyCheckRadius;
                            }
                            else
                            {
                                v = mobileParty2.Position;
                                float num2;
                                float num3;
                                if (!DistanceHelper.FindClosestDistanceFromMobilePartyToMobileParty(mobileParty, mobileParty2, mobileParty.NavigationCapability, getEncounterJoiningRadius * num * 10f, out num2, out num3))
                                {
                                    mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                                    continue;
                                }
                            }
                            float num4 = mobileParty.Position.Distance(v);
                            if (num4 >= getEncounterJoiningRadius * num * 3f)
                            {
                                mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                                continue;
                            }
                            if (bestInitiativeTargetParty != null && mobileParty.IsLordParty && !mobileParty2.IsLordParty && bestInitiativeBehavior == AiBehavior.EngageParty && bestInitiativeTargetParty.IsLordParty)
                            {
                                mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                                continue;
                            }
                            if (mobileParty2.MapEvent != null && (mobileParty2.MapEvent.IsBlockade || mobileParty2.MapEvent.IsBlockadeSallyOut) && !mobileParty.IsCurrentlyAtSea)
                            {
                                mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                                continue;
                            }
                            if (mobileParty.Army != null && mobileParty.AttachedTo == null)
                            {
                                if (mobileParty.Army.LeaderParty.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty2.SiegeEvent != null && mobileParty2.SiegeEvent.BesiegedSettlement == mobileParty.Army.LeaderParty.TargetSettlement)
                                {
                                    mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                                    continue;
                                }
                                if (mobileParty2.IsBandit)
                                {
                                    mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                                    continue;
                                }
                                MobileParty mobileParty3 = mobileParty2.AttachedTo ?? mobileParty2;
                                if (mobileParty.Army.LeaderParty != mobileParty)
                                {
                                    if (!mobileParty3.IsFleeing() || mobileParty3.ShortTermTargetParty != mobileParty.Army.LeaderParty)
                                    {
                                        Army army = mobileParty3.Army;
                                        if (((army != null) ? army.EstimatedStrength : mobileParty3.Party.EstimatedStrength) >= mobileParty.Army.EstimatedStrength)
                                        {
                                            goto IL_5B1;
                                        }
                                    }
                                    mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                                    continue;
                                }
                            }
                        IL_5B1:
                            float num5 = 1f + MathF.Max(0f, (num4 - 1f) / ((getEncounterJoiningRadius - 1f) * 2f));
                            num5 = ((num5 > num) ? num : num5);
                            float num6 = ((mobileParty.Army != null && (mobileParty.AttachedTo != null || mobileParty.Army.LeaderParty == mobileParty)) ? mobileParty.Army.EstimatedStrength : mobileParty.Party.EstimatedStrength) + 0.01f;
                            if (mobileParty2.IsCurrentlyAtSea != mobileParty.IsCurrentlyAtSea)
                            {
                                num6 = ((mobileParty.Army != null && (mobileParty.AttachedTo != null || mobileParty.Army.LeaderParty == mobileParty)) ? mobileParty.Army.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.SeaBattle) : mobileParty.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.SeaBattle)) + 0.01f;
                            }
                            float aggressiveness = mobileParty.Aggressiveness;
                            float num7 = 0f;
                            float num8 = 0.01f;
                            if (mobileParty2.BesiegerCamp != null)
                            {
                                bool isCurrentlyAtSea = mobileParty.IsCurrentlyAtSea;
                                MapEvent.PowerCalculationContext context = isCurrentlyAtSea ? MapEvent.PowerCalculationContext.SeaBattle : Campaign.Current.Models.MilitaryPowerModel.GetContextForPosition(mobileParty2.SiegeEvent.BesiegerCamp.LeaderParty.Position);
                                using (IEnumerator<PartyBase> enumerator = mobileParty2.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(isCurrentlyAtSea ? MapEvent.BattleTypes.BlockadeBattle : MapEvent.BattleTypes.Siege).GetEnumerator())
                                {
                                    while (enumerator.MoveNext())
                                    {
                                        PartyBase partyBase = enumerator.Current;
                                        num8 += partyBase.GetCustomStrength(BattleSideEnum.Defender, context);
                                    }
                                    goto IL_77E;
                                }
                                goto IL_726;
                            }
                            goto IL_726;
                        IL_77E:
                            bool flag = false;
                            LocatableSearchData<MobileParty> locatableSearchData2 = MobileParty.StartFindingLocatablesAroundPosition(mobileParty.Position.ToVec2(), getEncounterJoiningRadius * (num + 1f));
                            MobileParty mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                            float num9 = 0f;
                            while (mobileParty4 != null)
                            {
                                if ((mobileParty.MapFaction == mobileParty4.MapFaction && mobileParty4.BesiegedSettlement != null) || (mobileParty4.MapEvent != null && mobileParty4.MapEvent != mobileParty2.MapEvent))
                                {
                                    mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                                }
                                else if (mobileParty4.AttachedTo != null)
                                {
                                    mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                                }
                                else if (mobileParty4.IsCurrentlyAtSea != mobileParty.IsCurrentlyAtSea)
                                {
                                    mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                                }
                                else if (mobileParty4.IsInRaftState)
                                {
                                    mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                                }
                                else if (mobileParty4.CurrentSettlement != null && mobileParty4.CurrentSettlement.SiegeEvent != null)
                                {
                                    mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                                }
                                else
                                {
                                    if (mobileParty4.ShortTermBehavior == AiBehavior.EngageParty && mobileParty4.ShortTermTargetParty == mobileParty && mobileParty4.MapFaction != mobileParty2.MapFaction)
                                    {
                                        flag = true;
                                        break;
                                    }
                                    if (mobileParty4 != mobileParty && mobileParty4 != mobileParty2)
                                    {
                                        Vec2 v2 = (mobileParty4.BesiegedSettlement != null) ? mobileParty4.VisualPosition2DWithoutError : mobileParty4.Position.ToVec2();
                                        float num10 = (mobileParty4 != mobileParty2) ? v2.Distance(v.ToVec2()) : mobileParty.Position.Distance(v2);
                                        if (num10 > num * getEncounterJoiningRadius)
                                        {
                                            mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                                            continue;
                                        }
                                        if (mobileParty4.BesiegerCamp != null && mobileParty4.BesiegerCamp.LeaderParty != mobileParty4)
                                        {
                                            mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                                            continue;
                                        }
                                        if (mobileParty4.IsGarrison || mobileParty4.IsMilitia)
                                        {
                                            mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                                            continue;
                                        }
                                        PartyBase aiBehaviorPartyBase = mobileParty4.Ai.AiBehaviorPartyBase;
                                        if (mobileParty4.Army != null)
                                        {
                                            aiBehaviorPartyBase = mobileParty4.Army.LeaderParty.Ai.AiBehaviorPartyBase;
                                        }
                                        bool flag2 = aiBehaviorPartyBase != null && (aiBehaviorPartyBase == mobileParty2.Party || (aiBehaviorPartyBase.MapEvent != null && aiBehaviorPartyBase.MapEvent == mobileParty2.Party.MapEvent));
                                        bool flag3 = (mobileParty.Army != null && mobileParty.Army == mobileParty4.Army && mobileParty.Army.DoesLeaderPartyAndAttachedPartiesContain(mobileParty)) || (mobileParty2.Army != null && mobileParty2.Army == mobileParty4.Army) || (mobileParty2.BesiegedSettlement != null && mobileParty2.BesiegedSettlement == mobileParty4.BesiegedSettlement) || (num4 > getEncounterJoiningRadius && flag2) || (num10 > getEncounterJoiningRadius && flag2 && mobileParty2 != MobileParty.MainParty && (MobileParty.MainParty.Army == null || mobileParty2 != MobileParty.MainParty.Army.LeaderParty));
                                        if (flag3 || num10 < getEncounterJoiningRadius * num5)
                                        {
                                            float num11 = flag3 ? 1f : ((num10 < getEncounterJoiningRadius) ? 1f : (1f - (num10 - getEncounterJoiningRadius) / (getEncounterJoiningRadius * (num5 - 1f))));
                                            num11 = MathF.Min(1f, num11);
                                            bool flag4 = mobileParty2.MapEvent != null && mobileParty2.MapEvent == mobileParty4.MapEvent;
                                            float num12 = (mobileParty4.Army != null && (mobileParty4.AttachedTo != null || mobileParty4.Army.LeaderParty == mobileParty4)) ? mobileParty4.Army.EstimatedStrength : mobileParty4.Party.EstimatedStrength;
                                            if (mobileParty4.IsGarrison && !mobileParty.IsLordParty)
                                            {
                                                num9 += MathF.Max(mobileParty4.Party.EstimatedStrength, 250f);
                                            }
                                            if ((mobileParty4.Aggressiveness > 0.01f || mobileParty4.IsGarrison || flag4) && mobileParty4.MapFaction == mobileParty2.MapFaction)
                                            {
                                                if (mobileParty4.BesiegerCamp != null)
                                                {
                                                    using (IEnumerator<PartyBase> enumerator = mobileParty4.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(mobileParty.IsCurrentlyAtSea ? MapEvent.BattleTypes.BlockadeBattle : MapEvent.BattleTypes.Siege).GetEnumerator())
                                                    {
                                                        while (enumerator.MoveNext())
                                                        {
                                                            PartyBase partyBase2 = enumerator.Current;
                                                            bool flag5 = mobileParty.DefaultBehavior == AiBehavior.DefendSettlement && partyBase2.SiegeEvent.BesiegedSettlement == mobileParty.TargetSettlement;
                                                            num9 += partyBase2.EstimatedStrength * (flag5 ? 0.2f : 1f);
                                                        }
                                                        goto IL_BF5;
                                                    }
                                                }
                                                num9 += num12 * num11;
                                            }
                                        IL_BF5:
                                            if (mobileParty.MapFaction == mobileParty4.MapFaction && !mobileParty4.IsMainParty)
                                            {
                                                bool flag6 = mobileParty4.Aggressiveness > 0.01f || (mobileParty4.CurrentSettlement != null && mobileParty4.CurrentSettlement == mobileParty2.CurrentSettlement);
                                                bool flag7 = mobileParty2 != MobileParty.MainParty || Campaign.Current.Models.MobilePartyAIModel.ShouldConsiderAttacking(mobileParty4, MobileParty.MainParty);
                                                bool flag8 = mobileParty4.CurrentSettlement == null || !mobileParty4.CurrentSettlement.IsHideout;
                                                if (flag4 || (flag6 && flag7 && flag8))
                                                {
                                                    Settlement currentSettlement2 = mobileParty4.CurrentSettlement;
                                                    if (((currentSettlement2 != null) ? currentSettlement2.SiegeEvent : null) == null || mobileParty2 != mobileParty4.CurrentSettlement.SiegeEvent.BesiegerCamp.LeaderParty)
                                                    {
                                                        if (mobileParty4.BesiegerCamp != null)
                                                        {
                                                            using (IEnumerator<PartyBase> enumerator = mobileParty4.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).GetEnumerator())
                                                            {
                                                                while (enumerator.MoveNext())
                                                                {
                                                                    PartyBase partyBase3 = enumerator.Current;
                                                                    num6 += partyBase3.EstimatedStrength;
                                                                    if (partyBase3.MobileParty.Aggressiveness > aggressiveness)
                                                                    {
                                                                        aggressiveness = partyBase3.MobileParty.Aggressiveness;
                                                                    }
                                                                }
                                                                goto IL_D6A;
                                                            }
                                                        }
                                                        num6 += num12 * num11;
                                                        if (mobileParty4.Aggressiveness > aggressiveness)
                                                        {
                                                            aggressiveness = mobileParty4.Aggressiveness;
                                                        }
                                                        if (mobileParty4.CurrentSettlement != null)
                                                        {
                                                            num7 += num12 * num11;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                IL_D6A:
                                    mobileParty4 = MobileParty.FindNextLocatable(ref locatableSearchData2);
                                }
                            }
                            num8 += num9 * 0.9f;
                            if (mobileParty.CurrentSettlement != null)
                            {
                                num6 -= num7;
                            }
                            if (mobileParty2.LastVisitedSettlement != null && mobileParty2.LastVisitedSettlement.IsVillage && mobileParty2.Position.DistanceSquared(mobileParty2.LastVisitedSettlement.Position) < 1f && mobileParty2.LastVisitedSettlement.MapFaction.IsAtWarWith(mobileParty.MapFaction))
                            {
                                num8 += 20f;
                            }
                            float num13 = num6 / num8;
                            num13 *= (((mobileParty.IsCaravan || mobileParty.IsVillager) && mobileParty2 == MobileParty.MainParty) ? 0.6f : 1f);
                            num13 *= (mobileParty.IsPatrolParty ? (mobileParty2.IsBandit ? 1.2f : (mobileParty2.IsLordParty ? 0.9f : (mobileParty2.IsPatrolParty ? 0.8f : 1f))) : 1f);
                            if (mobileParty2.IsCaravan && mobileParty.LeaderHero != null && mobileParty.LeaderHero.IsMinorFactionHero)
                            {
                                num13 *= 1.5f;
                            }
                            if (mobileParty2.MapEvent != null && mobileParty2.MapEvent.IsSiegeAssault && mobileParty2 == mobileParty2.MapEvent.AttackerSide.LeaderParty.MobileParty)
                            {
                                float settlementAdvantage = Campaign.Current.Models.CombatSimulationModel.GetSettlementAdvantage(mobileParty2.MapEvent.MapEventSettlement);
                                if (num7 * MathF.Sqrt(settlementAdvantage) > num8)
                                {
                                    mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                                    continue;
                                }
                            }
                            float num14;
                            float num15;
                            CalculateInitiativeScoresForEnemy(mobileParty, mobileParty2, out num14, out num15, num13, aggressiveness);
                            if (flag)
                            {
                                num15 = 0f;
                            }
                            if (mobileParty2.CurrentSettlement != null && mobileParty2.MapEvent == null)
                            {
                                num15 = 0f;
                            }
                            if (num13 > 2f && mobileParty.Army != null && mobileParty.Army.LeaderParty == mobileParty && mobileParty2.AttachedParties.Count == 0 && !mobileParty.Army.IsWaitingForArmyMembers() && (mobileParty.DefaultBehavior != AiBehavior.GoAroundParty || mobileParty.TargetParty != mobileParty2))
                            {
                                num15 = 0f;
                                num14 = 0f;
                            }
                            if (num14 > 1f)
                            {
                                MobilePartyAi.DangerousPartiesAndTheirVecs.Add(new ValueTuple<float, Vec2>(num14, (v.ToVec2() - mobileParty.Position.ToVec2()).Normalized()));
                            }
                            if (num14 > bestInitiativeBehaviorScore || (num14 * 0.75f > bestInitiativeBehaviorScore && bestInitiativeBehavior == AiBehavior.EngageParty))
                            {
                                bestInitiativeBehavior = AiBehavior.FleeToPoint;
                                bestInitiativeTargetParty = mobileParty2;
                                bestInitiativeBehaviorScore = num14;
                            }
                            if (num15 > bestInitiativeBehaviorScore && (bestInitiativeBehaviorScore < num15 * 0.75f || bestInitiativeBehavior == AiBehavior.EngageParty))
                            {
                                bestInitiativeBehavior = AiBehavior.EngageParty;
                                bestInitiativeTargetParty = mobileParty2;
                                bestInitiativeBehaviorScore = num15;
                            }
                            mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                            continue;
                        IL_726:
                            if (mobileParty2.CurrentSettlement == null || !mobileParty2.CurrentSettlement.IsUnderSiege)
                            {
                                num8 += ((mobileParty2.Army != null && (mobileParty2.AttachedTo != null || mobileParty2.Army.LeaderParty == mobileParty2)) ? mobileParty2.Army.EstimatedStrength : mobileParty2.Party.EstimatedStrength);
                                goto IL_77E;
                            }
                            goto IL_77E;
                        }
                    }
                    mobileParty2 = MobileParty.FindNextLocatable(ref locatableSearchData);
                }
            }
        }
        if (bestInitiativeBehavior == AiBehavior.FleeToPoint || bestInitiativeBehavior == AiBehavior.FleeToGate)
        {
            float num16 = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vec2 v3 = new Vec2(MathF.Sin((float)i / 8f * 3.1415927f * 2f), MathF.Cos((float)i / 8f * 3.1415927f * 2f));
                float num17 = 0f;
                for (int j = 0; j < MobilePartyAi.DangerousPartiesAndTheirVecs.Count; j++)
                {
                    Vec2 item = MobilePartyAi.DangerousPartiesAndTheirVecs[j].Item2;
                    float num18 = item.DistanceSquared(v3);
                    if (num18 > 1f)
                    {
                        num18 = 1f + (num18 - 1f) * 0.5f;
                    }
                    num17 += num18 * MobilePartyAi.DangerousPartiesAndTheirVecs[j].Item1;
                }
                if (num17 > num16)
                {
                    averageEnemyVec = -v3;
                    num16 = num17;
                }
            }
        }
    }

    private static bool IsEnemy(PartyBase party, MobileParty mobileParty)
    {
        return FactionManager.IsAtWarAgainstFaction(party.MapFaction, mobileParty.MapFaction);
    }

    private static void CalculateInitiativeScoresForEnemy(MobileParty mobileParty, MobileParty enemyParty, out float avoidScore, out float attackScore, float localAdvantage, float maxAggressiveness)
    {
        attackScore = 0f;
        avoidScore = 0f;
        float num = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius * 1.2f;
        CampaignVec2 campaignVec = mobileParty.Position;
        if (!mobileParty.IsCurrentlyAtSea && enemyParty.IsCurrentlyAtSea)
        {
            campaignVec = mobileParty.CurrentSettlement.PortPosition;
        }
        float length = (enemyParty.Position.ToVec2() - campaignVec.ToVec2()).Length;
        float num2 = CalculateStanceScore(mobileParty, enemyParty);
        float num3 = MBMath.ClampFloat(0.5f * (1f + localAdvantage), 0.05f, 3f);
        float num4 = MBMath.ClampFloat((localAdvantage < 1f) ? MBMath.ClampFloat(1f / localAdvantage, 0.05f, 3f) : 0f, 0.05f, 3f);
        if (Campaign.Current.Models.MobilePartyAIModel.ShouldConsiderAttacking(mobileParty, enemyParty) && num3 > num4)
        {
            float initiativeDistanceForAttack = GetInitiativeDistanceForAttack(mobileParty, enemyParty, num);
            float num5 = 1f;
            float num6 = (mobileParty.IsBandit && mobileParty.HasNavalNavigationCapability) ? 10f : 5f;
            if (length < Campaign.Current.Models.EncounterModel.NeededMaximumDistanceForEncounteringMobileParty * num6 || (mobileParty.Army != null && mobileParty.Army.LeaderParty == mobileParty && enemyParty.Army != null && enemyParty.Army.LeaderParty == enemyParty && initiativeDistanceForAttack * 2f > length))
            {
                num5 = 100f;
            }
            else if (enemyParty.IsMoving && enemyParty.SiegeEvent == null && enemyParty.MapEvent == null)
            {
                float num7 = mobileParty.LastCalculatedBaseSpeed - enemyParty.LastCalculatedBaseSpeed;
                if (num7 > 0.01f)
                {
                    float num8 = initiativeDistanceForAttack / num7;
                    float num9 = (float)CampaignTime.HoursInDay * 0.75f;
                    if (num8 < num9)
                    {
                        num5 = num9 / num8;
                    }
                }
                else
                {
                    num5 = 0f;
                }
            }
            float num10 = (enemyParty.IsLordParty && enemyParty.LeaderHero != null && enemyParty.LeaderHero.IsLord) ? 1f : mobileParty.Ai.AttackInitiative;
            if ((double)mobileParty.Aggressiveness < 0.01)
            {
                maxAggressiveness = mobileParty.Aggressiveness;
            }
            float num11 = (enemyParty.MapEvent != null && maxAggressiveness > 0.1f) ? MathF.Max(1f + (enemyParty.MapEvent.IsSallyOut ? 0.3f : 0f), maxAggressiveness) : maxAggressiveness;
            float num12 = (mobileParty.DefaultBehavior == AiBehavior.DefendSettlement && ((enemyParty.BesiegedSettlement != null && mobileParty.Ai.AiBehaviorPartyBase == enemyParty.BesiegedSettlement.Party) || (enemyParty.MapEvent != null && enemyParty.MapEvent.MapEventSettlement != null && mobileParty.Ai.AiBehaviorPartyBase == enemyParty.MapEvent.MapEventSettlement.Party))) ? 1.1f : 1f;
            float num13 = 1f;
            if (mobileParty.IsLordParty && mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint && num3 * 0.8f > num4)
            {
                MobileParty.NavigationType navigationType = mobileParty.HasNavalNavigationCapability ? MobileParty.NavigationType.All : MobileParty.NavigationType.Default;
                num13 += 0.2f * (Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigationType) * 0.5f) / mobileParty.AiBehaviorTarget.Distance(mobileParty.Position);
            }
            float num14 = (enemyParty.MapEvent != null && enemyParty.MapEventSide.OtherSide.Parties.ContainsQ((MapEventParty x) => x.Party.IsMobile && x.Party.MapFaction == mobileParty.MapFaction)) ? 1.2f : 1f;
            attackScore = 1.06f * num12 * num3 * num2 * num5 * num11 * num13 * num10 * num14;
        }
        if (attackScore < 1f)
        {
            if (enemyParty.IsGarrison)
            {
                attackScore = 0f;
                if (enemyParty == mobileParty.ShortTermTargetParty)
                {
                    mobileParty.RecalculateShortTermBehavior();
                }
            }
            if (Campaign.Current.Models.MobilePartyAIModel.ShouldConsiderAvoiding(mobileParty, enemyParty))
            {
                float num15 = (mobileParty.IsCaravan || mobileParty.IsVillager) ? 0.9f : ((enemyParty.IsGarrison || enemyParty.IsMilitia || enemyParty.CurrentSettlement != null) ? 0.4f : 0.7f);
                float num16 = num * num15;
                if (enemyParty.MapEvent != null || enemyParty.BesiegedSettlement != null || (mobileParty.DefaultBehavior == AiBehavior.EngageParty && mobileParty.TargetParty == enemyParty) || (mobileParty.DefaultBehavior == AiBehavior.GoAroundParty && mobileParty.TargetParty == enemyParty))
                {
                    num16 = num * 0.6f;
                }
                num16 *= (1f + mobileParty.Ai.AvoidInitiative) / 2f;
                float num17 = 1f;
                if (length < num16 * 4f)
                {
                    float num18 = length / (num16 + 1E-05f);
                    num17 = 4f - num18;
                }
                float num19 = (enemyParty.IsLordParty && enemyParty.LeaderHero != null && enemyParty.LeaderHero.IsLord) ? 1f : mobileParty.Ai.AvoidInitiative;
                avoidScore = 0.9433963f * num19 * num17 * ((num2 > 0.01f) ? 1f : 0f) * num4;
            }
        }
    }

    private static float GetInitiativeDistanceForAttack(MobileParty mobileParty, MobileParty enemyParty, float reasonableDistance)
    {
        float num = 1f;
        if (enemyParty.IsCaravan)
        {
            num = (mobileParty.IsBandit ? 2f : ((mobileParty.Army == null) ? 1.5f : 1f));
        }
        else if (enemyParty.Aggressiveness < 0.1f)
        {
            num = 0.7f;
        }
        else if (enemyParty.IsBandit || enemyParty.IsLordParty)
        {
            num = ((mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint) ? 3.5f : 1f);
        }
        else if ((mobileParty.DefaultBehavior == AiBehavior.GoAroundParty || mobileParty.ShortTermBehavior == AiBehavior.GoAroundParty) && enemyParty != mobileParty.TargetParty)
        {
            num = 0.7f;
        }

        if (mobileParty.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty.TargetSettlement is null)
        {
            Logger.Error("Party {partyId}: {var} was null", mobileParty.StringId, nameof(mobileParty.TargetSettlement));
            return reasonableDistance * num;
        }

        if (enemyParty.MapEvent == null && mobileParty._lastCalculatedSpeed < enemyParty._lastCalculatedSpeed * 1.1f && (mobileParty.DefaultBehavior != AiBehavior.GoAroundParty || mobileParty.TargetParty != enemyParty) && (mobileParty.DefaultBehavior != AiBehavior.DefendSettlement || enemyParty != mobileParty.TargetSettlement.LastAttackerParty))
        {
            float b = MathF.Max(0.5f, (mobileParty._lastCalculatedSpeed + 0.1f) / (enemyParty._lastCalculatedSpeed + 0.1f)) / 1.1f;
            num *= MathF.Max(0.8f, b) * MathF.Max(0.8f, b);
        }
        float num2 = reasonableDistance * num;
        num2 *= (1f + ((mobileParty.Army != null && mobileParty.Army.LeaderParty != null && (enemyParty.BesiegedSettlement == mobileParty.Army.LeaderParty.TargetSettlement || (mobileParty.Army.LeaderParty.TargetSettlement != null && enemyParty == mobileParty.Army.LeaderParty.TargetSettlement.LastAttackerParty))) ? 1f : mobileParty.Ai.AttackInitiative)) / 2f;
        num2 *= ((enemyParty.Army != null) ? MathF.Pow((float)enemyParty.Army.Parties.Count, 0.33f) : 1f);
        if (enemyParty.MapEvent != null || enemyParty.BesiegedSettlement != null || (mobileParty.DefaultBehavior == AiBehavior.EngageParty && mobileParty.TargetParty == enemyParty) || (mobileParty.DefaultBehavior == AiBehavior.GoAroundParty && mobileParty.TargetParty == enemyParty))
        {
            num2 = reasonableDistance * 1.5f;
        }
        return num2;
    }

    // Token: 0x060018C6 RID: 6342 RVA: 0x00079769 File Offset: 0x00077969
    private static float CalculateStanceScore(MobileParty mobileParty, MobileParty otherParty)
    {
        if (FactionManager.IsAtWarAgainstFaction(mobileParty.MapFaction, otherParty.MapFaction))
        {
            return 1f;
        }
        if (DiplomacyHelper.IsSameFactionAndNotEliminated(mobileParty.MapFaction, otherParty.MapFaction))
        {
            return -1f;
        }
        return 0f;
    }
}
