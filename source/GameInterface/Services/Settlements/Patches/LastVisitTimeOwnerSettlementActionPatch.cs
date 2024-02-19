using Common.Util;
using GameInterface.Services.MobileParties.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyDebug]
[HarmonyPatch(typeof(EnterSettlementAction))]
public class LastVisitTimeOwnerSettlementActionPatch
{
    private static FieldInfo Settlement_LastVisitTimeOwner = typeof(Settlement).GetField("LastVisitTimeOfOwner");

    private static MethodInfo SettlementActionSetLastVisitTimeOfOwnerOverride =
        typeof(LastVisitTimeOwnerSettlementActionPatch).GetMethod(nameof(SetLastVisitTimeOfOwner), BindingFlags.NonPublic | BindingFlags.Static);


    static MethodBase TargetMethod()
    {
        var privateTypeEnterSettlementDetail = AccessTools.Inner(typeof(EnterSettlementAction), "EnterSettlementDetail");
        return AccessTools.Method(typeof(EnterSettlementAction), "ApplyInternal",
            new[] {
                typeof(Hero),
                typeof(MobileParty),
                typeof(Settlement),
                privateTypeEnterSettlementDetail,
                typeof(object),
                typeof(bool)
            });
    }


    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {

        int i = -1;
        foreach (var instruction in instructions)
        {
            // 67
            i++;
            if (instruction.opcode == OpCodes.Stfld && instruction.operand as FieldInfo == Settlement_LastVisitTimeOwner)
            {
                yield return new CodeInstruction(OpCodes.Call, SettlementActionSetLastVisitTimeOfOwnerOverride);
                continue;
            }
            yield return instruction;
        }
    }


    private static void SetLastVisitTimeOfOwner(Settlement instance, float newValue)
    { 

        if (ModInformation.IsServer || AllowedThread.IsThisThreadAllowed())
        {
            instance.LastVisitTimeOfOwner = newValue;
            // TODO: publish to clients.
            return;
        }
    }
}
