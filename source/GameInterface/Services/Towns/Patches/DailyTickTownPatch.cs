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
    // Get the PropertyInfo for the FoodStocks property
    private static PropertyInfo foodStocksProperty = typeof(Fief).GetProperty("FoodStocks");

    // Get the setter method for the FoodStocks property
    private static MethodInfo foodStocksSetter = foodStocksProperty.GetSetMethod(true);

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
            yield return instruction;
        }
        
    }

    public static void InterceptSetFoodStock(Fief fief, float value)
    {
 
        if (ModInformation.IsClient) return;
        fief.FoodStocks = value; // The message broker will be called by the prefix patch
    }
}
