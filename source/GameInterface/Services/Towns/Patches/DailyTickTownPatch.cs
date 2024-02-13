using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Encounters;
using System.Linq;
using static HarmonyLib.Code;
using Common.Messaging;
using GameInterface.Services.Fiefs.Messages;
using Newtonsoft.Json.Linq;
using Autofac.Core.Activators;
using GameInterface;
using GameInterface.Services.Fiefs.Patches;

[HarmonyPatch(typeof(Town), "DailyTick")]
public static class TownDailyTickPatch
{

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instrs)
    {
        
        foreach (var instruction in instrs)
        {
            if (instruction.opcode == Call.opcode && instruction.operand as MethodInfo == AccessTools.PropertySetter(typeof(Fief), "FoodStocks"))
            {
                
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TownDailyTickPatch), "InterceptSetFoodStock"));
                
                continue;
            }
            
            if (instruction.opcode == Call.opcode && instruction.operand as MethodInfo == AccessTools.PropertySetter(typeof(Town), "Prosperity"))
            {

                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TownDailyTickPatch), "InterceptSetProsperity"));

                continue;
            }
            yield return instruction;
        }
        
    }

    public static void InterceptSetFoodStock(Fief fief, float value)
    {
 
        if (ModInformation.IsClient) return;
        fief.FoodStocks = value; // The message broker will be called by the prefix patch
    }

    public static void InterceptSetProsperity(Town town, float value)
    {
        if (ModInformation.IsClient) return;
        town.Prosperity = value; // The message broker will be called by the prefix patch
    }
}
