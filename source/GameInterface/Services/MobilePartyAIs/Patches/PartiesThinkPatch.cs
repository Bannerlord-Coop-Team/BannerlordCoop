using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
            Campaign.Current.Models.MobilePartyAIModel.GetBestInitiativeBehavior(ai._mobileParty, out var bestInitiativeBehavior, out var bestInitiativeTargetParty, out var bestInitiativeBehaviorScore, out averageEnemyVec);
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
}
