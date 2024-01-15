using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyDebug]
    [HarmonyPatch(typeof(DefaultPartySpeedCalculatingModel))]
    [HarmonyPatch(nameof(DefaultPartySpeedCalculatingModel.CalculateBaseSpeed))]
    internal class CalculateBaseSpeedPatch 
    {
        //static FieldInfo f_someField = AccessTools.Field(typeof(SomeType), nameof(SomeType.someField));
        //static MethodInfo m_MyExtraMethod = SymbolExtensions.GetMethodInfo(() => Tools.MyExtraMethod());
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instr = instructions.ToList();


            instr[405].opcode = OpCodes.Nop;
            instr[406].opcode = OpCodes.Nop;
            instr[407].opcode = OpCodes.Nop;
            instr[408].opcode = OpCodes.Nop;
            instr[409].opcode = OpCodes.Nop;
            instr[410].opcode = OpCodes.Nop;

            return instr.AsEnumerable();
            //yield return new CodeInstruction(OpCodes.Call, )
        }

        private static void CalculateBaseSpeed(ref MobileParty mobileParty, ref ExplainedNumber __result)
        {
            /*
            if(ModInformation.IsServer && mobileParty.IsPartyControlled() == false)
            {
                float playerMapMovementSpeedBonusMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerMapMovementSpeedBonusMultiplier();
                if (playerMapMovementSpeedBonusMultiplier > 0f)
                {
                    __result.AddFactor(playerMapMovementSpeedBonusMultiplier, new TaleWorlds.Localization.TextObject(0f));
                }
            }
            */
        }
    }
}
