using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Keeps camp changes valid when siege callbacks detach members of the same army.
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.BesiegerCamp), MethodType.Setter)]
internal class MobilePartyBesiegerCampPatches
{
    [HarmonyPrefix]
    private static void Prefix(MobileParty __instance, out bool __state)
    {
        __state = __instance._besiegerCampResetStarted;
    }

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var attachedParties = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._attachedParties));
        var snapshot = AccessTools.Method(typeof(MobilePartyBesiegerCampPatches), nameof(SnapshotAttachedParties));
        bool replaced = false;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.LoadsField(attachedParties))
            {
                // A child's camp change can remove it from the parent's live attachment list.
                yield return new CodeInstruction(OpCodes.Call, snapshot);
                replaced = true;
            }
        }

        if (!replaced)
            throw new InvalidOperationException("Unable to snapshot attached parties during a siege camp change");
    }

    private static MBList<MobileParty> SnapshotAttachedParties(MBList<MobileParty> parties)
    {
        return new MBList<MobileParty>(parties);
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(MobileParty __instance, bool __state, Exception __exception)
    {
        // Preserve an outer call's guard, but do not leave a failed transition permanently latched.
        if (__exception != null && !__state)
            __instance._besiegerCampResetStarted = false;

        return __exception;
    }
}
