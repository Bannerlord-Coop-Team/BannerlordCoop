using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Data;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal class MobilePartyCollectionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyCollectionPatches>();

    static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(MobileParty));
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var addMethod = typeof(List<MobileParty>).GetMethod("Add");
        var addIntercept = typeof(MobilePartyCollectionPatches).GetMethod(nameof(AddIntercept));

        var removeMethod = typeof(List<MobileParty>).GetMethod("Remove");
        var removeIntercept = typeof(MobilePartyCollectionPatches).GetMethod(nameof(RemoveIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == addMethod)
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, addIntercept);
            }
            else if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == removeMethod)
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, removeIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void AddIntercept(List<MobileParty> _attachedParties, MobileParty addParty, MobileParty instance)
    {
        // Allows original method call if this thread is allowed
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            _attachedParties.Add(addParty);
            return;
        }

        // Skip method if called from client and allow origin
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            _attachedParties.Add(addParty);
            return;
        }

        var data = new AttachedPartyData(instance.StringId, addParty.StringId);
        MessageBroker.Instance.Publish(instance, new AttachedPartyAdded(data));

        _attachedParties.Add(addParty);
    }

    public static bool RemoveIntercept(List<MobileParty> _attachedParties, MobileParty removeParty, MobileParty instance)
    {
        // Allows original method call if this thread is allowed
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return _attachedParties.Remove(removeParty);
        }

        // Skip method if called from client and allow origin
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            return _attachedParties.Remove(removeParty);
        }

        var data = new AttachedPartyData(instance.StringId, removeParty.StringId);
        MessageBroker.Instance.Publish(instance, new AttachedPartyRemoved(data));

        return _attachedParties.Remove(removeParty);
    }
}
