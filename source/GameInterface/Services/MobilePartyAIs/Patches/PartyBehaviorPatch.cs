using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases;
using HarmonyLib;
using SandBox.View.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MobilePartyAIs.Patches;

/// <summary>
/// Handles changes in party behavior for the <see cref="MobilePartyAi"/> behavior synchronization system.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[HarmonyPatch(typeof(MobilePartyAi))]
static class PartyBehaviorPatch
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
        if (ModInformation.DISABLE_AI == false) return true;

        // This disables AI
        return __instance.DefaultBehaviorNeedsUpdate;
    }

    [HarmonyPrefix]
    [HarmonyPatch("SetAiBehavior")]
    private static bool SetAiBehaviorPrefix(
        ref MobilePartyAi __instance,
        ref AiBehavior newAiBehavior,
        ref IInteractablePoint interactablePoint,
        ref CampaignVec2 bestTargetPoint)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (__instance._mobileParty != Campaign.Current.MainParty && BehaviorIsSame(ref __instance, ref newAiBehavior, ref interactablePoint, ref bestTargetPoint)) return false;

        if (__instance._mobileParty.IsPartyControlled() == false) return false;

        var message = new PartyBehaviorChangeAttempted(__instance, newAiBehavior, interactablePoint, bestTargetPoint);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    private static bool BehaviorIsSame(
        ref MobilePartyAi __instance,
        ref AiBehavior newAiBehavior,
        ref IInteractablePoint interactablePoint,
        ref CampaignVec2 bestTargetPoint)
    {
        MobileParty party = __instance._mobileParty;
        IMapPoint targetEntity = null;

        if (interactablePoint != null)
        {
            if (interactablePoint is AnchorPoint anchorPoint)
            {
                targetEntity = anchorPoint.Owner;
            }
            else if (interactablePoint is PartyBase targetParty)
            {
                targetEntity = targetParty.MobileParty;
            }
        }

        return __instance.BehaviorTarget == targetEntity?.Position &&
            party.ShortTermBehavior == newAiBehavior &&
            __instance.BehaviorTarget == bestTargetPoint;

    }
}


/// <summary>
/// Patches for the methods of MapCameraView class.
/// </summary>
[HarmonyPatch(typeof(MapCameraView))]
public static class MapCameraViewPatches
{
    private static readonly FieldInfo LabelNumberField = typeof(Label).GetField("m_label", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Replaces the instructions in the else if (mainParty.Ai.ForceAiNoPathMode) block with Nop instructions.
    /// </summary>
    /// <param name="instructions">instructions of the patched method.</param>
    /// <returns></returns>
    //[HarmonyPatch(nameof(MapCameraView.OnBeforeTick))]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> OnBeforeTickPatch(IEnumerable<CodeInstruction> instructions)
    //{
    //    bool FoundGoTo = false;
    //    bool FoundLabel = false;
    //    foreach (CodeInstruction instruction in instructions)
    //    {
    //        bool isGoTo33 = (instruction.opcode == OpCodes.Brfalse_S && instruction.operand is Label && (int)LabelNumberField.GetValue(instruction.operand) == 33);
    //        FoundGoTo = FoundGoTo || isGoTo33;
    //        FoundLabel = FoundLabel || (instruction.opcode == OpCodes.Ldarg_0 && instruction.labels.Any(label=> (int)LabelNumberField.GetValue(label) == 33));
    //        if (FoundGoTo && !FoundLabel && !isGoTo33)
    //        {
    //            yield return new CodeInstruction(OpCodes.Nop);
    //        }
    //        else
    //        {
    //            yield return instruction;
    //        }
    //    }
    //}
}