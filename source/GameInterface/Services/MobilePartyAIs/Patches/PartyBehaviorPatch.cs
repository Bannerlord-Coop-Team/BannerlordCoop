using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;

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
        ref CampaignVec2 bestTargetPoint)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (BehaviorIsSame(ref __instance, ref newAiBehavior, ref interactablePoint, ref bestTargetPoint)) return false;

        if (__instance._mobileParty.IsControlledByThisInstance() == false) return false;
        var message = new PartyBehaviorChangeAttempted(__instance, newAiBehavior, interactablePoint, bestTargetPoint);
        MessageBroker.Instance.Publish(__instance, message);

        if (MobilePartyAiConfig.DEBUG && ModInformation.IsServer)
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

        // Clients apply their own behavior immediately; the server still replicates it to observers.
        return ModInformation.IsClient;
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

    public static void SetAiBehavior(
        MobilePartyAi partyAi, AiBehavior newBehavior, IInteractablePoint interactablePoint, CampaignVec2 targetPoint)
    {
        if (partyAi == null)
        {
            var callStack = Environment.StackTrace;

            Logger.Error("PartyAI was null\n{stacktrace}", callStack);
            return;
        }

        using (new AllowedThread())
        {

            var mobileParty = partyAi._mobileParty;

            if (interactablePoint is PartyBase partyBase)
            {
                partyAi.AiBehaviorPartyBase = partyBase;
            }
            else
            {
                partyAi.AiBehaviorPartyBase = null;
            }

            try
            {
                mobileParty.TargetPosition = targetPoint;
                mobileParty.SetShortTermBehavior(newBehavior, interactablePoint);

                partyAi.AiBehaviorInteractable = interactablePoint;
                partyAi.BehaviorTarget = targetPoint;

                partyAi.UpdateBehavior();
            }
            catch(Exception ex)
            {
                Logger.Error(ex, "Failed to update party behavior for {StringId}", mobileParty.StringId);
            }
        }
    }
}