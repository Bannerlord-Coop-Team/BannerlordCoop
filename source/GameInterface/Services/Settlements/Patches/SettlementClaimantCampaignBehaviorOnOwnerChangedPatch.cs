using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Settlements;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Common.Util;
using Common.Messaging;
using GameInterface.Services.Settlements.Messages;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Transpiler for SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged() to change Settlement.CanBeClaimed setter
/// </summary>
[HarmonyPatch(typeof(SettlementClaimantCampaignBehavior))]
public class SettlementClaimantCampaignBehaviorOnOwnerChangedPatch
{
    private static FieldInfo Settlement_CanBeClaimed = typeof(Settlement).GetField("CanBeClaimed");

    private static MethodInfo SettlementBehaviorSetCanBeClaimed =
        typeof(SettlementClaimantCampaignBehaviorOnOwnerChangedPatch)
        .GetMethod(nameof(SetCanBeClaimed), BindingFlags.NonPublic | BindingFlags.Static);

    [HarmonyPatch("OnSettlementOwnerChanged")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Stfld && instruction.operand as FieldInfo == Settlement_CanBeClaimed)
            {
                yield return new CodeInstruction(OpCodes.Call, SettlementBehaviorSetCanBeClaimed);
                continue;
            }
            yield return instruction;
        }
    }

    private static void SetCanBeClaimed(Settlement instance, int canBeClaimed)
    {
        if (AllowedThread.IsThisThreadAllowed())
        {
            instance.CanBeClaimed = canBeClaimed;
            return;
        }

        if (ModInformation.IsClient) return;
        instance.CanBeClaimed = canBeClaimed;
        MessageBroker.Instance.Publish(instance, new SettlementClaimantCanBeClaimedChanged(instance.StringId, canBeClaimed));
    }

    internal static void RunCanBeClaimed(Settlement settlement, int canBeClaimed)
    {
        using (new AllowedThread())
        {
            settlement.CanBeClaimed = canBeClaimed;
        }
    }
}
