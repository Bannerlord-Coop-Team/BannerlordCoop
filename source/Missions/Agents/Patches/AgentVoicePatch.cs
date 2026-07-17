using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Agents.Messages;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches;

/// <summary>
/// Lets the per-mission handler replace a local order voice before native FMOD randomizes it.
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.MakeVoice), new[]
{
    typeof(SkinVoiceManager.SkinVoiceType),
    typeof(SkinVoiceManager.CombatVoiceNetworkPredictionType)
})]
[HarmonyPatchCategory(MissionModule.AgentVoicePatchCategory)]
internal static class AgentVoicePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Agent __instance, SkinVoiceManager.SkinVoiceType voiceType)
    {
        if (AllowedThread.IsThisThreadAllowed() || !OrderVoiceContextPatch.IsActive) return true;

        var voice = new AgentVoicePlayed(__instance, voiceType.TypeID);
        MessageBroker.Instance.Publish(__instance, voice);
        return !voice.Handled;
    }
}
