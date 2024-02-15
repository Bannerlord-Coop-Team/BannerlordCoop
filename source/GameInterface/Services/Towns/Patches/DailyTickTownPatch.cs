using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using static HarmonyLib.Code;
using GameInterface;


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
