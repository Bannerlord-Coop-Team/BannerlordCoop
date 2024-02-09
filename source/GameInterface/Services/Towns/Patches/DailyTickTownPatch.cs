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
        //List<CodeInstruction> instructions = instrs.ToList();
        //int setterLastIdx = instructions.FindLastIndex(i => i.opcode == Call.opcode && i.operand as MethodInfo == foodStocksSetter);
        //if (setterLastIdx == -1) return instrs;
        foreach (var instruction in instrs)
        {
            if (instruction.opcode == Call.opcode && instruction.operand as MethodInfo == foodStocksSetter)
            {
                //instructions.RemoveAt(i);
                //instructions.RemoveAt(i-1);
                //instructions.InsertRange(i, new CodeInstruction[]
                //{
                //   new CodeInstruction(OpCodes.Ldarg_0),
                //  new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TownDailyTickPatch), "LogFoodStock"))
                //});

                //yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TownDailyTickPatch), "InterceptSetFoodStock"));
                
                continue;
            }
            yield return instruction;
        }
        
    }

    public static void InterceptSetFoodStock(Fief fief, float value)
    {
        // If it's the client, return
        if (!ModInformation.IsServer) return;
        fief.FoodStocks = value;
        //FiefPatches.ChangeFiefFoodStock(fief, value);
        //Console.WriteLine("FoodStocks changed to " + value + "for the fief " + fief.StringId);
        //var message = new FiefFoodStockChanged(fief.StringId,value);
        //MessageBroker.Instance.Publish(fief, message);

    }
}
