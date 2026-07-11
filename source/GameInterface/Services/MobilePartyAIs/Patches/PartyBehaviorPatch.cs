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

            // A raw map click can land on terrain the party can never stand on — settlement
            // footprints are RuralArea/Mountain paint, and MobileParty.ComputePath early-outs
            // on an invalid-terrain target WITHOUT producing a path, after which the party
            // walks a straight pathless line through everything (players ran through towns).
            // Vanilla's map screen clamps clicks to accessible ground before ordering; this
            // pipeline replaces that ordering, so clamp here the same way. The query is native
            // on graphical clients (honors disabled navmesh faces) and grid-backed headless.
            if (interactablePoint is null && Campaign.Current?.MapSceneWrapper is IMapScene scene
                && Campaign.Current.Models?.PartyNavigationModel is { } navModel)
            {
                bool needsClamp = !targetPoint.IsValid()
                    || !navModel.IsTerrainTypeValidForNavigationType(
                        scene.GetFaceTerrainType(targetPoint.Face), mobileParty.NavigationCapability);
                if (needsClamp)
                {
                    targetPoint = scene.GetAccessiblePointNearPosition(targetPoint, 32f);
                }
            }

            // ComputePath ALSO early-outs (pathless straight-line movement again) when the
            // party's OWN position is invalid — e.g. parked on the RuralArea gate ring or a
            // hair over a river bank. An order is the moment to heal that: nudge to the
            // nearest accessible ground (native on clients, grid-backed headless). Only fires
            // on an invalid stand — parties on walkable ground are untouched.
            if (!mobileParty.Position.IsValid() && Campaign.Current?.MapSceneWrapper is IMapScene healScene)
            {
                var healed = healScene.GetAccessiblePointNearPosition(mobileParty.Position, 8f);
                if (healed.IsValid())
                {
                    Logger.Information(
                        "Healing invalid position for {Party} at ({X:0.#},{Y:0.#}) -> ({HX:0.#},{HY:0.#}) on order",
                        mobileParty.StringId, mobileParty.Position.X, mobileParty.Position.Y, healed.X, healed.Y);
                    mobileParty.Position = healed;
                }
            }

            mobileParty.DefaultBehavior = newBehavior;


            if (interactablePoint is null)
            {
                mobileParty._targetSettlement = null;
                mobileParty._targetParty = null;
                partyAi.AiBehaviorPartyBase = null;
            }

            if (interactablePoint is PartyBase partyBase)
            {
                if (partyBase.IsSettlement)
                {
                    mobileParty._targetSettlement = partyBase.Settlement;
                    mobileParty._targetParty = null;
                    partyAi.AiBehaviorPartyBase = partyBase;
                }
                else if (partyBase.IsMobile)
                {
                    mobileParty._targetSettlement = null;
                    mobileParty._targetParty = partyBase.MobileParty;
                    partyAi.AiBehaviorPartyBase = partyBase;
                }
            }


            try
            {
                mobileParty.TargetPosition = targetPoint;
                mobileParty.SetShortTermBehavior(newBehavior, interactablePoint);

                partyAi.AiBehaviorInteractable = interactablePoint;
                partyAi.BehaviorTarget = targetPoint;

                mobileParty.RecalculateShortTermBehavior();
                partyAi.UpdateBehavior();
            }
            catch(Exception ex)
            {
                Logger.Error(ex, "Failed to update party behavior for {StringId}", mobileParty.StringId);
            }
        }
    }
}