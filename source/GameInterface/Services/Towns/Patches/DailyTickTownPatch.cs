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

[HarmonyPatch(typeof(Town), "DailyTick")]
public static class TownDailyTickPatch
{
    // Get the PropertyInfo for the FoodStocks property
    private static PropertyInfo foodStocksProperty = typeof(Fief).GetProperty("FoodStocks");

    // Get the setter method for the FoodStocks property
    private static MethodInfo foodStocksSetter = foodStocksProperty.GetSetMethod(true);

    [HarmonyPatch(typeof(Town), "DailyTick")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instrs)
    {
        List<CodeInstruction> instructions = instrs.ToList();
        int setterLastIdx = instructions.FindLastIndex(i => i.opcode == Call.opcode && i.operand as MethodInfo == foodStocksSetter);
        if (setterLastIdx == -1) return instrs;

        instructions.InsertRange(setterLastIdx, new CodeInstruction[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TownDailyTickPatch), "LogFoodStock"))
        });
        
        return instructions;
        
    }

    public static void LogFoodStock(Fief fief)
    {
        // If it's the client, return
        if (!ModInformation.IsServer) return;
        
        var message = new FiefFoodStockChanged(fief.StringId, fief.FoodStocks);
        MessageBroker.Instance.Publish(fief, message);
    }
}
