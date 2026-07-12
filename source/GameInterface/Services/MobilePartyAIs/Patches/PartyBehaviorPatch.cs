using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobilePartyAIs.Patches;

/// <summary>
/// Handles changes in party behavior for the <see cref="MobilePartyAi"/> behavior synchronization system.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[HarmonyPatch(typeof(MobilePartyAi))]
public static class PartyBehaviorPatch
{
    static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAi>();

    /// <summary>
    /// This prevents the tick method being called without the need for an update
    /// Likely speeds the game up quite a bit lmao
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("Tick")]
    private static bool TickPrefix(ref MobilePartyAi __instance)
    {
        if (MobilePartyAiConfig.ENABLED) return true;
            
        // This disables AI
        return __instance._mobileParty == MobileParty.MainParty;
    }

    [HarmonyPrefix]
    [HarmonyPatch("SetAiBehavior")]
    private static bool SetAiBehaviorPrefix(
        ref MobilePartyAi __instance,
        ref AiBehavior newAiBehavior,
        ref IInteractablePoint interactablePoint,
        ref CampaignVec2 bestTargetPoint,
        out bool __state)
    {
        __state = false;

        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (BehaviorIsSame(ref __instance, ref newAiBehavior, ref interactablePoint, ref bestTargetPoint)) return false;

        if (__instance._mobileParty.IsControlledByThisInstance() == false) return false;

        // The authority keeps the original suppression contract: route the request through the handler,
        // which invokes vanilla once under AllowedThread and snapshots the resulting state. A controlling
        // client runs vanilla locally first and publishes from the postfix so prediction captures the real
        // DesiredAiNavigationType and MoveTargetPoint rather than their pre-call values.
        if (ModInformation.IsServer)
        {
            PublishBehaviorAttempt(__instance, newAiBehavior, interactablePoint, bestTargetPoint,
                stateAlreadyApplied: false);

            if (MobilePartyAiConfig.DEBUG)
            {
                if (interactablePoint is null)
                {
                    Logger.Debug("Pre-update. PartyId: {partyId}, Behavior: {behavior}, Target: {target}", __instance._mobileParty.StringId, newAiBehavior, null);
                }

                if (interactablePoint is PartyBase partyBase)
                {
                    Logger.Debug("Pre-update. PartyId: {partyId}, Behavior: {behavior}, Target: {target}", __instance._mobileParty.StringId, newAiBehavior,
                        partyBase.IsSettlement ? partyBase.Settlement.StringId : partyBase.MobileParty.StringId);
                }
            }

            return false;
        }

        __state = true;
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetAiBehavior")]
    private static void SetAiBehaviorPostfix(
        MobilePartyAi __instance,
        AiBehavior newAiBehavior,
        IInteractablePoint interactablePoint,
        CampaignVec2 bestTargetPoint,
        bool __state)
    {
        if (!__state) return;

        PublishBehaviorAttempt(__instance, newAiBehavior, interactablePoint, bestTargetPoint,
            stateAlreadyApplied: true);
    }

    private static void PublishBehaviorAttempt(
        MobilePartyAi partyAi,
        AiBehavior newAiBehavior,
        IInteractablePoint interactablePoint,
        CampaignVec2 bestTargetPoint,
        bool stateAlreadyApplied)
    {
        var message = new PartyBehaviorChangeAttempted(
            partyAi,
            newAiBehavior,
            interactablePoint,
            bestTargetPoint,
            stateAlreadyApplied);
        MessageBroker.Instance.Publish(partyAi, message);
    }

    private static bool BehaviorIsSame(
        ref MobilePartyAi __instance,
        ref AiBehavior newAiBehavior,
        ref IInteractablePoint interactablePoint,
        ref CampaignVec2 bestTargetPoint)
    {
        var party = __instance._mobileParty;

        return __instance._aiBehaviorInteractable == interactablePoint &&
            party.ShortTermBehavior == newAiBehavior &&
            __instance.BehaviorTarget == bestTargetPoint;

    }

    public static bool ApplyBehaviorSnapshot(
        MobilePartyAi partyAi,
        PartyBehaviorUpdateData data,
        IInteractablePoint interactablePoint,
        MobileParty targetParty,
        Settlement targetSettlement,
        MobileParty moveTargetParty)
    {
        if (partyAi == null)
        {
            var callStack = Environment.StackTrace;

            Logger.Error("PartyAI was null\n{stacktrace}", callStack);
            return false;
        }

        using (new AllowedThread())
        {
            var mobileParty = partyAi._mobileParty;

            try
            {
                // Default behavior recalculates short-term behavior immediately, so its target
                // dependencies must be installed first. SetShortTermBehavior can in turn change
                // DesiredAiNavigationType; restore the authoritative navigation type afterwards.
                mobileParty.SetTargetSettlement(targetSettlement, data.IsTargetingPort);
                mobileParty.TargetParty = targetParty;
                mobileParty.TargetPosition = data.TargetPosition;
                mobileParty.DefaultBehavior = data.DefaultBehavior;
                mobileParty.SetShortTermBehavior(data.NewAiBehavior, interactablePoint);
                mobileParty.DesiredAiNavigationType = data.DesiredAiNavigationType;

                partyAi.BehaviorTarget = data.BestTargetPoint;

                partyAi.UpdateBehavior();
                ApplyNavigationSnapshot(mobileParty, data, moveTargetParty);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update party behavior for {StringId}", mobileParty?.StringId);
                return false;
            }
        }
    }

    private static void ApplyNavigationSnapshot(
        MobileParty mobileParty,
        PartyBehaviorUpdateData data,
        MobileParty moveTargetParty)
    {
        // Use vanilla setters to keep private path state aligned with the authoritative mode.
        switch (data.PartyMoveMode)
        {
            case MoveModeType.Hold:
                mobileParty.SetNavigationModeHold();
                break;
            case MoveModeType.Point:
                mobileParty.SetNavigationModePoint(data.MoveTargetPoint);
                break;
            case MoveModeType.Party when moveTargetParty != null:
                mobileParty.SetNavigationModeParty(moveTargetParty);
                break;
            default:
                mobileParty.PartyMoveMode = data.PartyMoveMode;
                mobileParty.MoveTargetParty = moveTargetParty;
                break;
        }

        mobileParty.MoveTargetPoint = data.MoveTargetPoint;
        mobileParty.NextTargetPosition = data.NextTargetPosition;
    }
}
