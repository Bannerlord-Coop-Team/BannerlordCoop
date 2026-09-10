using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
public static class ClanIncomeVMPatches
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ClanIncomeVM), nameof(ClanIncomeVM.RefreshList));
        yield return AccessTools.Method(typeof(ClanIncomeVM), nameof(ClanIncomeVM.RefreshAlleys));
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> RefreshTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var mainHero = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.MainHero));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(mainHero))
            {
                instruction.operand = AccessTools.Method(typeof(ClanIncomeVMPatches), nameof(GetClanLeader));
            }
            else if (instruction.opcode == OpCodes.Newobj && instruction.operand is ConstructorInfo constructor)
            {
                if (constructor.DeclaringType == typeof(ClanFinanceWorkshopItemVM))
                {
                    instruction.operand = AccessTools.Constructor(typeof(CoopClanWorkshopItemVM),
                        constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
                }
                else if (constructor.DeclaringType == typeof(ClanFinanceAlleyItemVM))
                {
                    instruction.operand = AccessTools.Constructor(typeof(CoopClanAlleyItemVM),
                        constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
                }
            }

            yield return instruction;
        }
    }

    private static Hero GetClanLeader() => Clan.PlayerClan.Leader;
}
