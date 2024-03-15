using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Hero))]
[HarmonyDebug]
public class HeroChildrenPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroChildrenPatches>();

    [HarmonyPatch(nameof(Hero.Father), MethodType.Setter)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Father(IEnumerable<CodeInstruction> instructions)
    {
        var listAddMethod = AccessTools.Method(typeof(List<Hero>), "Add");
        var listAddOverrideMethod = AccessTools.Method(typeof(HeroChildrenPatches), nameof(ListAddOverride));

        foreach (var instruction in instructions)
        {
            // Find List<Hero>.Add in the intermediate language instructions (MSIL)
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == listAddMethod)
            {
                // Load instance onto stack
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                // Replace our add call with our intercept function (line above adds instance to the parameters, specifically as the last parameter by adding it to the stack)
                yield return new CodeInstruction(OpCodes.Call, listAddOverrideMethod);
            }
            else
            {
                // Return original instruction if it is not the one we are looking for
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(nameof(Hero.Mother), MethodType.Setter)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ExSpousesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var listAddMethod = AccessTools.Method(typeof(List<Hero>), "Add");
        var listAddOverrideMethod = AccessTools.Method(typeof(HeroChildrenPatches), nameof(ListAddOverride));

        foreach (var instruction in instructions)
        {
            // Find List<Hero>.Add in the intermediate language instructions (MSIL)
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == listAddMethod)
            {
                // Load instance onto stack
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                // Replace our add call with our intercept function (line above adds instance to the parameters, specifically as the last parameter by adding it to the stack)
                yield return new CodeInstruction(OpCodes.Call, listAddOverrideMethod);
            }
            else
            {
                // Return original instruction if it is not the one we are looking for
                yield return instruction;
            }
        }
    }

    public static void ListAddOverride(MBList<Hero> _children, Hero child, Hero father)
    {
        // Allows original method call if this thread is allowed
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            _children.Add(child);
            return;
        }

        // Skip method if called from client and allow origin
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            _children.Add(child);
            return;
        }

        MessageBroker.Instance.Publish(father, new NewChildrenAdded(father.StringId, child.StringId));

        _children.Add(child);
    }

}
