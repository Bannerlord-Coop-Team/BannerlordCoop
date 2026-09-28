#if DEBUG
using Common;
using Common.Commands;
using GameInterface;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Diagnostics;

internal static class AgentHitSoundDiagnostics
{
    private const int MaximumEvents = 512;
    private static readonly object Gate = new object();
    private static readonly List<object> Events = new List<object>();
    private static Mission observedMission;
    private static string instanceId;
    private static string controllerId;
    private static bool recording;
    private static int dropped;
    private static int sequence;

    internal static bool Start()
    {
        Mission mission = Mission.Current;
        var session = mission?.GetMissionBehavior<CoopBattleController>()?.Session;
        if (mission == null || session == null || !BattleSpawnGate.IsCoopBattleActive)
            return false;

        lock (Gate)
        {
            observedMission = mission;
            instanceId = session.InstanceId;
            controllerId = session.OwnControllerId;
            Events.Clear();
            dropped = 0;
            sequence = 0;
            recording = true;
        }
        return true;
    }

    internal static string Snapshot(bool stop)
    {
        lock (Gate)
        {
            if (stop) recording = false;
            return JsonConvert.SerializeObject(new
            {
                instanceId,
                controllerId,
                active = recording,
                sameMission = ReferenceEquals(observedMission, Mission.Current),
                dropped,
                events = Events.ToArray()
            });
        }
    }

    internal static void RecordBlow(Agent victim, Blow blow, AttackCollisionData collisionData)
    {
        Mission mission = victim?.Mission;
        if (!ShouldRecord(mission) || victim == null ||
            !ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry) ||
            !registry.TryGetAgentInfo(victim, out CoopAgentInfo info) ||
            !registry.IsLocallyControlled(info.AgentId))
            return;

        Agent attacker = blow.OwnerId < 0 ? null : mission.FindAgentWithIndex(blow.OwnerId);
        Add(new
        {
            kind = "registerBlow",
            victimId = info.AgentId.ToString("D"),
            attackerId = AgentId(registry, attacker),
            victimIndex = victim.Index,
            attackerIndex = blow.OwnerId,
            sourceControllerId = BattleSpawnGate.RoutedBlowSourceControllerId,
            damage = blow.InflictedDamage,
            blockedWithShield = collisionData.AttackBlockedWithShield,
            missile = blow.IsMissile,
            x = blow.GlobalPosition.x,
            y = blow.GlobalPosition.y,
            z = blow.GlobalPosition.z
        });
    }

    internal static void RecordSound(Mission mission, int soundIndex, Vec3 position,
        int relatedAgent1, int relatedAgent2, SoundEventParameter parameter)
    {
        if (!ShouldRecord(mission) || parameter.ParamName != "Armor Type") return;

        INetworkAgentRegistry registry = null;
        ContainerProvider.TryResolve(out registry);
        Agent attacker = relatedAgent1 < 0 ? null : mission.FindAgentWithIndex(relatedAgent1);
        Agent victim = relatedAgent2 < 0 ? null : mission.FindAgentWithIndex(relatedAgent2);
        Add(new
        {
            kind = "makeSoundCompleted",
            soundIndex,
            armorType = parameter.Value,
            relatedAgent1,
            relatedAgent2,
            attackerId = AgentId(registry, attacker),
            victimId = AgentId(registry, victim),
            x = position.x,
            y = position.y,
            z = position.z
        });
    }

    private static bool ShouldRecord(Mission mission)
    {
        lock (Gate)
            return recording && ReferenceEquals(observedMission, mission) &&
                BattleSpawnGate.IsCoopBattleActive;
    }

    private static string AgentId(INetworkAgentRegistry registry, Agent agent) =>
        registry != null && agent != null &&
        registry.TryGetAgentInfo(agent, out CoopAgentInfo info)
            ? info.AgentId.ToString("D") : null;

    private static void Add(object value)
    {
        lock (Gate)
        {
            if (!recording) return;
            if (Events.Count >= MaximumEvents)
            {
                dropped++;
                return;
            }
            Events.Add(new { sequence = ++sequence, utc = DateTime.UtcNow, value });
        }
    }
}

[HarmonyPatch(typeof(Agent), nameof(Agent.RegisterBlow))]
[HarmonyPatchCategory(MissionModule.CombatHitPresentationPatchCategory)]
internal static class AgentHitSoundBlowDiagnosticPatch
{
    private static void Postfix(Agent __instance, Blow blow, ref AttackCollisionData collisionData) =>
        AgentHitSoundDiagnostics.RecordBlow(__instance, blow, collisionData);
}

[HarmonyPatch(typeof(Mission), nameof(Mission.MakeSound),
    new Type[] { typeof(int), typeof(Vec3), typeof(bool), typeof(bool), typeof(int), typeof(int), typeof(SoundEventParameter) },
    new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal,
        ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref })]
[HarmonyPatchCategory(MissionModule.CombatHitPresentationPatchCategory)]
internal static class AgentHitSoundNativeDiagnosticPatch
{
    private static void Postfix(Mission __instance, int soundIndex, Vec3 position,
        int relatedAgent1, int relatedAgent2, ref SoundEventParameter parameter) =>
        AgentHitSoundDiagnostics.RecordSound(__instance, soundIndex, position,
            relatedAgent1, relatedAgent2, parameter);
}

public sealed class AgentHitSoundTraceCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.battle";
    public string Name => "hit_sound_trace";
    public string Description => "Captures native battle hit-sound calls and authoritative blows.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "start, snapshot or stop")
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsServer || args.Count != 1)
            return new CoopCommandResult(false, "Run start, snapshot or stop on a client.", "command_failed");
        switch (args[0])
        {
            case "start":
                if (!AgentHitSoundDiagnostics.Start())
                    return new CoopCommandResult(false, "No active co-op battle mission.", "command_failed");
                return new CoopCommandResult(true, "Hit-sound trace started.");
            case "snapshot":
                return new CoopCommandResult(true,
                    "LIVE_TEST_JSON=" + AgentHitSoundDiagnostics.Snapshot(stop: false));
            case "stop":
                return new CoopCommandResult(true,
                    "LIVE_TEST_JSON=" + AgentHitSoundDiagnostics.Snapshot(stop: true));
            default:
                return new CoopCommandResult(false, "Expected start, snapshot or stop.", "command_failed");
        }
    }
}
#endif
