using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch]
[HarmonyDebug]
internal class HeroSpecialItemsCollectionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroSpecialItemsCollectionPatch>();
    
    [HarmonyPatch(typeof(SearchBodyMissionHandler), methodName: "AddItemsToPlayer")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ExSpousesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var listAddMethod = AccessTools.Method(typeof(List<ItemObject>), "Add");
        var listAddOverrideMethod = AccessTools.Method(typeof(HeroSpecialItemsCollectionPatch), nameof(ListAddOverride));

        foreach (var instruction in instructions)
        {
            // Find List<Hero>.Add in the intermediate language instructions (MSIL)
            // TODO: not sure on itemobject the sync at all
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


    public static void ListAddOverride(MBList<ItemObject> _specialItems, ItemObject specialItem, Hero instance)
    {
        // Allows original method call if this thread is allowed
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            _specialItems.Add(specialItem);
            return;
        }

        // Skip method if called from client and allow origin
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            _specialItems.Add(specialItem);
            return;
        }

        //MessageBroker.Instance.Publish(instance, new ExSpouseAdded(instance, exSpouse));

        _specialItems.Add(specialItem);
    }
    
}
