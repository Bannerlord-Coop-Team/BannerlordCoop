using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.ViewModelCollection.Conversation;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

[HarmonyPatch(typeof(MissionConversationVM))]
internal class PlayerPartyInteractionConversationVMPatches
{
    [HarmonyPatch(nameof(MissionConversationVM.Refresh))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RefreshTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var isCurrentlyPlayerSpeaking = AccessTools.Field(typeof(MissionConversationVM), "_isCurrentlyPlayerSpeaking");
        var shouldSkipAnswerOptions = AccessTools.Method(typeof(PlayerPartyInteractionConversationVMPatches), nameof(ShouldSkipAnswerOptions));

        foreach (var instruction in instructions)
        {
            yield return instruction;

            if (instruction.opcode == OpCodes.Ldsfld && Equals(instruction.operand, isCurrentlyPlayerSpeaking))
                yield return new CodeInstruction(OpCodes.Call, shouldSkipAnswerOptions);
        }
    }

    private static bool ShouldSkipAnswerOptions(bool isCurrentlyPlayerSpeaking)
    {
        if (PlayerPartyInteractionDialogState.HasActiveState)
            return false;

        return isCurrentlyPlayerSpeaking;
    }
}
