using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using Common.Util;
using Common.Messaging;
using GameInterface.Services.Settlements.Messages;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Transpiler to patch Settlement.Claimedby and Settlement.ClaimValue 
/// inside LordConversationsCampaignBehavior.conversation_player_ask_to_claim_land_answer_on_consequence()
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
public class ClaimLandAnswerOnConversationLordConversationsCampaignBehaviourPatch
{
    private static FieldInfo Settlement_ClaimedBy = typeof(Settlement).GetField("ClaimedBy");
    private static FieldInfo Settlement_ClaimValue = typeof(Settlement).GetField("ClaimValue");

    private static MethodInfo SettlementBehaviorSetLastClaimedBy =
        typeof(ClaimLandAnswerOnConversationLordConversationsCampaignBehaviourPatch)
        .GetMethod(nameof(SetClaimedBy), BindingFlags.NonPublic | BindingFlags.Static);

    private static MethodInfo SettlementBehaviorSetLastClaimValue =
    typeof(ClaimLandAnswerOnConversationLordConversationsCampaignBehaviourPatch)
    .GetMethod(nameof(SetClaimValue), BindingFlags.NonPublic | BindingFlags.Static);

    [HarmonyPatch("conversation_player_ask_to_claim_land_answer_on_consequence")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            // 67
            if (instruction.opcode == OpCodes.Stfld && instruction.operand as FieldInfo == Settlement_ClaimedBy)
            {
                yield return new CodeInstruction(OpCodes.Call, SettlementBehaviorSetLastClaimedBy);
                continue;
            }
            if (instruction.opcode == OpCodes.Stfld && instruction.operand as FieldInfo == Settlement_ClaimValue)
            {
                yield return new CodeInstruction(OpCodes.Call, SettlementBehaviorSetLastClaimValue);
                continue;
            }
            yield return instruction;
        }
    }

    internal static void RunClaimedBy(Settlement settlement, Hero hero)
    {
        using (new AllowedThread())
        {
            settlement.ClaimedBy = hero;
        }
    }
    private static void SetClaimedBy(Settlement instance, Hero hero)
    {

        if(AllowedThread.IsThisThreadAllowed())
        {
            instance.ClaimedBy = hero;
            return;
        }
        if (ModInformation.IsServer) return;

        instance.ClaimedBy = hero;

        MessageBroker.Instance.Publish(instance, new LordConversationCampaignBehaviourPlayerChangedClaim(instance.StringId, hero.StringId));

    }

    private static void SetClaimValue(Settlement instance, float claimValue)
    {

        if (AllowedThread.IsThisThreadAllowed())
        {
            instance.ClaimValue = claimValue;
            return;
        }
        if (ModInformation.IsServer) return;

        instance.ClaimValue = claimValue;
        MessageBroker.Instance.Publish(instance, new LordConversationCampaignBehaviourPlayerChangedClaimValue(instance.StringId, claimValue));
    }

    internal static void RunClaimedValue(Settlement settlement, float newValue)
    {
        using (new AllowedThread())
        {
            settlement.ClaimValue = newValue;
        }
    }

}
