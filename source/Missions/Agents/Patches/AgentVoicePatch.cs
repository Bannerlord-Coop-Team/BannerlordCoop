using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Agents.Messages;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches;

/// <summary>
/// Captures mission-agent voice events for the per-mission voice handler.
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.MakeVoice), new[]
{
    typeof(SkinVoiceManager.SkinVoiceType),
    typeof(SkinVoiceManager.CombatVoiceNetworkPredictionType)
})]
[HarmonyPatchCategory(MissionModule.AgentVoicePatchCategory)]
internal static class AgentVoicePatch
{
    [HarmonyPostfix]
    private static void Postfix(Agent __instance, SkinVoiceManager.SkinVoiceType voiceType)
    {
        if (AllowedThread.IsThisThreadAllowed()) return;

        MessageBroker.Instance.Publish(__instance, new AgentVoicePlayed(__instance, voiceType.TypeID));
    }
}
