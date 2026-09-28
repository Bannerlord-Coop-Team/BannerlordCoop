using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches;

[HarmonyPatch(typeof(Mission), nameof(Mission.MakeSound),
    new Type[] { typeof(int), typeof(Vec3), typeof(bool), typeof(bool), typeof(int), typeof(int), typeof(SoundEventParameter) },
    new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal,
        ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref })]
[HarmonyPatchCategory(MissionModule.CombatHitPresentationPatchCategory)]
public static class AgentHitSoundPatch
{
    private static void Postfix(Mission __instance, int soundIndex, Vec3 position,
        int relatedAgent1, int relatedAgent2, ref SoundEventParameter parameter)
    {
        if (!BattleSpawnGate.IsCoopBattleActive || parameter.ParamName != "Armor Type" || relatedAgent2 < 0)
            return;

        Agent victim = __instance.FindAgentWithIndex(relatedAgent2);
        if (victim == null) return;

        Agent attacker = relatedAgent1 < 0 ? null : __instance.FindAgentWithIndex(relatedAgent1);
        MessageBroker.Instance.Publish(victim,
            new AgentHitSound(victim, victim, attacker, soundIndex, position, parameter.Value));
    }
}
