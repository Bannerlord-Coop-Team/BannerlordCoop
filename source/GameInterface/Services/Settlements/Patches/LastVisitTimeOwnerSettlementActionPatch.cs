using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Patches EnterSettlementAction.ApplyInternal(Hero, MobileParty, Settlement, EnterSettlementDetail, obj, bool)
/// </summary>
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

        foreach (var instruction in instructions)
        {
            // 67
            if (instruction.opcode == OpCodes.Stfld && instruction.operand as FieldInfo == Settlement_LastVisitTimeOwner)
            {
                yield return new CodeInstruction(OpCodes.Call, SettlementActionSetLastVisitTimeOfOwnerOverride);
                continue;
            }
            yield return instruction;
        }
    }

    internal static void RunLastVisitTimeOfOwner(Settlement settlement, float currentTime)
    {
        using (new AllowedThread())
        {
            settlement.LastVisitTimeOfOwner = currentTime;
        }
    }

    private static void SetLastVisitTimeOfOwner(Settlement instance, float newValue)
    {

        if (AllowedThread.IsThisThreadAllowed())
        {
            instance.LastVisitTimeOfOwner = newValue;
            return;
        }
        if (ModInformation.IsClient) return;


        instance.LastVisitTimeOfOwner = newValue;
        MessageBroker.Instance.Publish(instance, new SettlementChangedLastVisitTimeOfOwner(instance.StringId, newValue));
    }
}
