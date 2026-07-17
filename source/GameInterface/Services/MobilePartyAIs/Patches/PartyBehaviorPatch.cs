using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
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
        ref CampaignVec2 bestTargetPoint,
        out bool __state)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            __state = false;
            return true;
        }

        __state = !BehaviorIsSame(__instance, newAiBehavior, interactablePoint, bestTargetPoint) &&
            __instance._mobileParty.IsControlledByThisInstance();
        if (!__state)
            return false;

        if (MobilePartyAiConfig.DEBUG && ModInformation.IsServer)
        {
            if (interactablePoint is null)
                Logger.Debug("Pre-update. PartyId: {partyId}, Behavior: {behavior}, Target: {target}", __instance._mobileParty.StringId, newAiBehavior, null);

            if (interactablePoint is PartyBase partyBase)
                Logger.Debug("Pre-update. PartyId: {partyId}, Behavior: {behavior}, Target: {target}", __instance._mobileParty.StringId, newAiBehavior,
                    partyBase.IsSettlement ? partyBase.Settlement.StringId : partyBase.MobileParty.StringId);
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetAiBehavior")]
    private static void SetAiBehaviorPostfix(
        MobilePartyAi __instance,
        bool __state)
    {
        if (!__state) return;

        MessageBroker.Instance.Publish(
            __instance,
            new PartyBehaviorChangeAttempted(__instance._mobileParty));
    }

    private static bool BehaviorIsSame(
        MobilePartyAi partyAi,
        AiBehavior behavior,
        IInteractablePoint interactable,
        CampaignVec2 target) =>
        partyAi._aiBehaviorInteractable == interactable &&
        partyAi._mobileParty.ShortTermBehavior == behavior &&
        partyAi.BehaviorTarget == target;

}
