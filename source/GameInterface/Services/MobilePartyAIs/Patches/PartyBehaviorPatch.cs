using Common.Extensions;
using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Map;
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
    static readonly Func<MobilePartyAi, bool> get_DefaultBehaviorNeedsUpdate = typeof(MobilePartyAi)
        .GetField("DefaultBehaviorNeedsUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildUntypedGetter<MobilePartyAi, bool>();
    static readonly Func<MobilePartyAi, MobileParty> _mobileParty = typeof(MobilePartyAi)
        .GetField("_mobileParty", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildUntypedGetter<MobilePartyAi, MobileParty>();

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
        return get_DefaultBehaviorNeedsUpdate(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("SetAiBehavior")]
    private static bool SetAiBehaviorPrefix(
        ref MobilePartyAi __instance,
        ref AiBehavior newAiBehavior,
        ref PartyBase targetPartyFigure,
        ref Vec2 bestTargetPoint)
    {
        if (BehaviorIsSame(ref __instance, ref newAiBehavior, ref targetPartyFigure, ref bestTargetPoint)) return false;

        MobileParty party = __instance._mobileParty;

        bool hasTargetEntity = false;
        string targetEntityId = string.Empty;

        if (targetPartyFigure != null)
        {
            hasTargetEntity = true;
            targetEntityId = targetPartyFigure.IsSettlement
                ? targetPartyFigure.Settlement.StringId
                : targetPartyFigure.MobileParty.StringId;
        }

        var data = new PartyBehaviorUpdateData(party.StringId, newAiBehavior, hasTargetEntity, targetEntityId, bestTargetPoint, party.Position2D);
        var message = new PartyBehaviorChangeAttempted(party, data);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    private static Func<MobilePartyAi, Vec2> get_MobilePartyAi_BehaviorTarget = typeof(MobilePartyAi)
        .GetField("BehaviorTarget", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildUntypedGetter<MobilePartyAi, Vec2>();
    private static bool BehaviorIsSame(
        ref MobilePartyAi __instance,
        ref AiBehavior newAiBehavior,
        ref PartyBase targetPartyFigure,
        ref Vec2 bestTargetPoint)
    {
        MobileParty party = __instance._mobileParty;
        IMapEntity targetEntity = null;

        if (targetPartyFigure != null)
        {
            targetEntity = targetPartyFigure.IsSettlement ? targetPartyFigure.MobileParty : targetPartyFigure.Settlement;
        }

        return __instance.AiBehaviorMapEntity == targetEntity &&
            party.ShortTermBehavior == newAiBehavior &&
            get_MobilePartyAi_BehaviorTarget(__instance) == bestTargetPoint;

    }

    public static void SetAiBehavior(
        MobilePartyAi partyAi, AiBehavior newBehavior, IMapEntity targetMapEntity, Vec2 targetPoint)
    {
        DefaultBehavior(partyAi, newBehavior);

        var mobileParty = _mobileParty(partyAi);

        if (typeof(Settlement).IsAssignableFrom(targetMapEntity?.GetType()))
        {
            TargetSettlement(mobileParty, (Settlement)targetMapEntity);
            TargetParty(mobileParty, null);
        }

        else if (typeof(MobileParty).IsAssignableFrom(targetMapEntity?.GetType()))
        {
            TargetSettlement(mobileParty, null);
            TargetParty(mobileParty, (MobileParty)targetMapEntity);
        }

        TargetPosition(mobileParty, targetPoint);

        SetShortTermBehavior(partyAi, newBehavior, targetMapEntity);
        SetBehaviorTarget(partyAi, targetPoint);
        UpdateBehavior(partyAi);
    }

    static readonly Action<MobilePartyAi, AiBehavior, IMapEntity> SetShortTermBehavior = typeof(MobilePartyAi)
        .GetMethod("SetShortTermBehavior", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildDelegate<Action<MobilePartyAi, AiBehavior, IMapEntity>>();

    static readonly Action<MobilePartyAi, Vec2> SetBehaviorTarget = typeof(MobilePartyAi)
        .GetField("BehaviorTarget", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildUntypedSetter<MobilePartyAi, Vec2>();

    static readonly Action<MobilePartyAi> UpdateBehavior = typeof(MobilePartyAi)
        .GetMethod("UpdateBehavior", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildDelegate<Action<MobilePartyAi>>();

    static readonly Action<MobilePartyAi, AiBehavior> DefaultBehavior = typeof(MobilePartyAi)
        .GetProperty(nameof(MobilePartyAi.DefaultBehavior)).GetSetMethod(true)
        .BuildDelegate<Action<MobilePartyAi, AiBehavior>>();

    static readonly Action<MobileParty, Settlement> TargetSettlement = typeof(MobileParty)
        .GetProperty(nameof(MobileParty.TargetSettlement)).GetSetMethod(true)
        .BuildDelegate<Action<MobileParty, Settlement>>();

    static readonly Action<MobileParty, MobileParty> TargetParty = typeof(MobileParty)
        .GetProperty(nameof(MobileParty.TargetParty)).GetSetMethod(true)
        .BuildDelegate<Action<MobileParty, MobileParty>>();

    static readonly Action<MobileParty, Vec2> TargetPosition = typeof(MobileParty)
        .GetProperty(nameof(MobileParty.TargetPosition)).GetSetMethod(true)
        .BuildDelegate<Action<MobileParty, Vec2>>();
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
    [HarmonyPatch(nameof(MapCameraView.OnBeforeTick))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OnBeforeTickPatch(IEnumerable<CodeInstruction> instructions)
    {
        bool FoundGoTo = false;
        bool FoundLabel = false;
        foreach (CodeInstruction instruction in instructions)
        {
            bool isGoTo33 = (instruction.opcode == OpCodes.Brfalse_S && instruction.operand is Label && (int)LabelNumberField.GetValue(instruction.operand) == 33);
            FoundGoTo = FoundGoTo || isGoTo33;
            FoundLabel = FoundLabel || (instruction.opcode == OpCodes.Ldarg_0 && instruction.labels.Any(label=> (int)LabelNumberField.GetValue(label) == 33));
            if (FoundGoTo && !FoundLabel && !isGoTo33)
            {
                yield return new CodeInstruction(OpCodes.Nop);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}