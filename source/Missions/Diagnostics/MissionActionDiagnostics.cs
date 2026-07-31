#if DEBUG
using Missions.Agents.Packets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Diagnostics;

internal readonly struct ActionPollMeasurement : IDisposable
{
    private readonly MissionActionDiagnostics.PollSample sample;

    public ActionPollMeasurement(MissionActionDiagnostics.PollSample sample)
    {
        this.sample = sample;
    }

    public void Dispose() => MissionActionDiagnostics.FinishPoll(sample);
}

internal readonly struct RemoteActionApplyMeasurement : IDisposable
{
    private readonly MissionActionDiagnostics.RemoteApplyContext context;

    public RemoteActionApplyMeasurement(
        MissionActionDiagnostics.RemoteApplyContext context)
    {
        this.context = context;
    }

    public void Dispose() =>
        MissionActionDiagnostics.FinishRemoteApply(context);
}

internal static class MissionActionDiagnostics
{
    private const int PreNative = 0;
    private const int PostNative = 1;
    private const int MovementChannel = 2;
    private const int TraceAgentsPerMovementClass = 32;
    private const int TraceAgentsPerActionClass = 16;
    private const int MaximumTimelineEvents = 20000;
    private const int MaximumRewindSamples = 64;
    private const float ProgressTolerance = 0.02f;

    private static readonly long[] PollTicks = new long[2];
    private static readonly long[] PollCalls = new long[2];
    private static readonly long[] PollVisits = new long[2];
    private static readonly long[] PollActiveVisits = new long[2];
    private static readonly long[] PollPlayerVisits = new long[2];
    private static readonly long[] PollUpdates = new long[2];
    private static readonly Dictionary<TraceKey, TraceTrack> Tracks =
        new Dictionary<TraceKey, TraceTrack>();
    private static readonly List<TraceEvent> Timeline =
        new List<TraceEvent>();
    private static readonly HashSet<Guid> TraceAgentIds =
        new HashSet<Guid>();
    private static readonly HashSet<Guid> AttackTraceAgentIds =
        new HashSet<Guid>();
    private static readonly HashSet<Guid> GuardTraceAgentIds =
        new HashSet<Guid>();
    private static readonly HashSet<Guid> WieldTraceAgentIds =
        new HashSet<Guid>();

    [ThreadStatic]
    private static PollSample currentPoll;
    [ThreadStatic]
    private static RemoteApplyContext currentRemoteApply;

    private static volatile bool performanceEnabled;
    private static long performanceStartedAt;
    private static long sentPackets;
    private static long sentUpdates;
    private static long sentBytes;
    private static long receivedPackets;
    private static long receivedUpdates;
    private static long receivedBytes;

    private static bool animationTraceEnabled;
    private static bool traceAgentsSelected;
    private static long traceStartedAt;
    private static int traceGeneration;
    private static long outboundUpdates;
    private static long remoteApplies;
    private static long actionCommands;
    private static long restartCommands;
    private static long sameActionRestarts;
    private static long rewindCommands;
    private static long guardCommands;
    private static long guardDirectionCommands;
    private static long beforeNativeGuardReplays;
    private static long afterMovementGuardReplays;
    private static long afterNativeGuardReplays;
    private static long mountActionCommands;
    private static long mountProgressReplays;
    private static long mountProgressWraps;
    private static long mountProgressRewinds;
    private static long sampledProgressDrops;

    internal sealed class PollSample
    {
        public readonly int Boundary;
        public readonly long StartedAt = Stopwatch.GetTimestamp();
        public int Visits;
        public int ActiveVisits;
        public int PlayerVisits;
        public int Updates;

        public PollSample(int boundary)
        {
            Boundary = boundary;
        }
    }

    internal sealed class RemoteApplyContext
    {
        public readonly Guid AgentId;
        public readonly long Sequence;

        public RemoteApplyContext(Guid agentId, long sequence)
        {
            AgentId = agentId;
            Sequence = sequence;
        }
    }

    private readonly struct TraceKey : IEquatable<TraceKey>
    {
        public readonly Guid AgentId;
        public readonly int Channel;

        public TraceKey(Guid agentId, int channel)
        {
            AgentId = agentId;
            Channel = channel;
        }

        public bool Equals(TraceKey other) =>
            AgentId == other.AgentId && Channel == other.Channel;

        public override bool Equals(object obj) =>
            obj is TraceKey other && Equals(other);

        public override int GetHashCode() =>
            (AgentId.GetHashCode() * 397) ^ Channel;
    }

    private sealed class TraceTrack
    {
        public Guid AgentId;
        public string Role;
        public string Category;
        public int Channel;
        public int ActionIndex;
        public Agent.ActionCodeType ActionType;
        public long StartedAt;
        public float LastProgress;
        public int Generation;
    }

    private sealed class TraceEvent
    {
        public double Milliseconds { get; set; }
        public double DurationMilliseconds { get; set; }
        public string Kind { get; set; }
        public string AgentId { get; set; }
        public string Role { get; set; }
        public string Category { get; set; }
        public int Channel { get; set; }
        public int ActionIndex { get; set; }
        public string ActionType { get; set; }
        public float Progress { get; set; }
        public float CurrentProgress { get; set; }
        public long Sequence { get; set; }
        public bool Restart { get; set; }
        public bool SameAction { get; set; }
    }

    public static bool PerformanceEnabled => performanceEnabled;
    public static bool AnimationTraceEnabled => animationTraceEnabled;

    public static ActionPollMeasurement MeasurePoll(bool afterNativeTick)
    {
        if (!performanceEnabled) return default;

        var sample = new PollSample(afterNativeTick ? PostNative : PreNative);
        currentPoll = sample;
        return new ActionPollMeasurement(sample);
    }

    public static void RecordPolledAgent(Agent agent, bool active)
    {
        PollSample sample = currentPoll;
        if (sample == null) return;

        sample.Visits++;
        if (!active) return;
        sample.ActiveVisits++;
        if (agent.Controller == AgentControllerType.Player)
            sample.PlayerVisits++;
    }

    public static void RecordActionUpdate()
    {
        if (currentPoll != null)
            currentPoll.Updates++;
    }

    internal static void FinishPoll(PollSample sample)
    {
        if (sample == null) return;
        if (ReferenceEquals(currentPoll, sample))
            currentPoll = null;

        int index = sample.Boundary;
        Interlocked.Add(
            ref PollTicks[index],
            Stopwatch.GetTimestamp() - sample.StartedAt);
        Interlocked.Increment(ref PollCalls[index]);
        Interlocked.Add(ref PollVisits[index], sample.Visits);
        Interlocked.Add(ref PollActiveVisits[index], sample.ActiveVisits);
        Interlocked.Add(ref PollPlayerVisits[index], sample.PlayerVisits);
        Interlocked.Add(ref PollUpdates[index], sample.Updates);
    }

    public static void RecordActionPacketSent(
        AgentActionPacket packet,
        int serializedBytes)
    {
        if (!performanceEnabled) return;
        Interlocked.Increment(ref sentPackets);
        Interlocked.Add(ref sentUpdates, packet.AgentIds?.Length ?? 0);
        Interlocked.Add(ref sentBytes, serializedBytes);
    }

    public static void RecordActionPacketReceived(
        AgentActionPacket packet,
        int serializedBytes)
    {
        if (!performanceEnabled) return;
        Interlocked.Increment(ref receivedPackets);
        Interlocked.Add(ref receivedUpdates, packet.AgentIds?.Length ?? 0);
        Interlocked.Add(ref receivedBytes, serializedBytes);
    }

    public static void StartPerformance()
    {
        performanceEnabled = false;
        for (int index = 0; index < 2; index++)
        {
            Interlocked.Exchange(ref PollTicks[index], 0);
            Interlocked.Exchange(ref PollCalls[index], 0);
            Interlocked.Exchange(ref PollVisits[index], 0);
            Interlocked.Exchange(ref PollActiveVisits[index], 0);
            Interlocked.Exchange(ref PollPlayerVisits[index], 0);
            Interlocked.Exchange(ref PollUpdates[index], 0);
        }
        Interlocked.Exchange(ref sentPackets, 0);
        Interlocked.Exchange(ref sentUpdates, 0);
        Interlocked.Exchange(ref sentBytes, 0);
        Interlocked.Exchange(ref receivedPackets, 0);
        Interlocked.Exchange(ref receivedUpdates, 0);
        Interlocked.Exchange(ref receivedBytes, 0);
        performanceStartedAt = Stopwatch.GetTimestamp();
        performanceEnabled = true;
    }

    public static string SnapshotPerformance(bool stop)
    {
        if (stop) performanceEnabled = false;

        long preVisits = Interlocked.Read(ref PollVisits[PreNative]);
        long oldEquivalentVisits =
            Interlocked.Read(ref PollVisits[PostNative]);
        long avoidedVisits = Math.Max(0, oldEquivalentVisits - preVisits);
        long now = Stopwatch.GetTimestamp();
        return JsonConvert.SerializeObject(new
        {
            enabled = performanceEnabled,
            wallMilliseconds = ToMilliseconds(
                now - performanceStartedAt),
            preNative = GetPollSnapshot(PreNative),
            postNative = GetPollSnapshot(PostNative),
            scanComparison = new
            {
                actualPreNativeVisits = preVisits,
                unoptimizedEquivalentVisits = oldEquivalentVisits,
                avoidedVisits,
                reductionPercent = oldEquivalentVisits == 0
                    ? 0d
                    : 100d * avoidedVisits / oldEquivalentVisits,
            },
            actionTraffic = new
            {
                sentPackets = Interlocked.Read(ref sentPackets),
                sentUpdates = Interlocked.Read(ref sentUpdates),
                sentSerializedBytes = Interlocked.Read(ref sentBytes),
                receivedPackets = Interlocked.Read(ref receivedPackets),
                receivedUpdates = Interlocked.Read(ref receivedUpdates),
                receivedSerializedBytes = Interlocked.Read(ref receivedBytes),
            },
        });
    }

    private static object GetPollSnapshot(int index)
    {
        long calls = Interlocked.Read(ref PollCalls[index]);
        double milliseconds =
            ToMilliseconds(Interlocked.Read(ref PollTicks[index]));
        return new
        {
            calls,
            milliseconds,
            averageMicroseconds = calls == 0
                ? 0d
                : 1000d * milliseconds / calls,
            registryVisits = Interlocked.Read(ref PollVisits[index]),
            activeVisits = Interlocked.Read(ref PollActiveVisits[index]),
            playerVisits = Interlocked.Read(ref PollPlayerVisits[index]),
            updates = Interlocked.Read(ref PollUpdates[index]),
        };
    }

    public static void StartAnimationTrace()
    {
        animationTraceEnabled = false;
        Tracks.Clear();
        Timeline.Clear();
        TraceAgentIds.Clear();
        AttackTraceAgentIds.Clear();
        GuardTraceAgentIds.Clear();
        WieldTraceAgentIds.Clear();
        traceAgentsSelected = false;
        traceGeneration = 0;
        outboundUpdates = 0;
        remoteApplies = 0;
        actionCommands = 0;
        restartCommands = 0;
        sameActionRestarts = 0;
        rewindCommands = 0;
        guardCommands = 0;
        guardDirectionCommands = 0;
        beforeNativeGuardReplays = 0;
        afterMovementGuardReplays = 0;
        afterNativeGuardReplays = 0;
        mountActionCommands = 0;
        mountProgressReplays = 0;
        mountProgressWraps = 0;
        mountProgressRewinds = 0;
        sampledProgressDrops = 0;
        traceStartedAt = Stopwatch.GetTimestamp();
        animationTraceEnabled = true;
    }

    public static void RecordOutboundAction(
        Guid agentId,
        AgentActionData data,
        long sequence)
    {
        if (!animationTraceEnabled) return;
        outboundUpdates++;
        AddEvent(
            "outbound-update",
            agentId,
            "owner",
            null,
            -1,
            data.Action0Index,
            null,
            data.Action0Progress,
            sequence,
            false,
            false);
    }

    public static RemoteActionApplyMeasurement MeasureRemoteApply(
        Guid agentId,
        long sequence)
    {
        if (!animationTraceEnabled) return default;
        remoteApplies++;
        var context = new RemoteApplyContext(agentId, sequence);
        currentRemoteApply = context;
        return new RemoteActionApplyMeasurement(context);
    }

    internal static void FinishRemoteApply(RemoteApplyContext context)
    {
        if (ReferenceEquals(currentRemoteApply, context))
            currentRemoteApply = null;
    }

    public static void RecordActionCommand(
        Agent agent,
        int channel,
        int targetActionIndex,
        float startProgress,
        AnimFlags animationFlags,
        string source)
    {
        RemoteApplyContext context = currentRemoteApply;
        RecordActionCommand(
            context?.AgentId ?? Guid.Empty,
            context?.Sequence ?? 0,
            agent,
            channel,
            targetActionIndex,
            startProgress,
            animationFlags,
            source);
    }

    public static void RecordExternalActionCommand(
        Guid agentId,
        Agent agent,
        int channel,
        int targetActionIndex,
        float startProgress,
        AnimFlags animationFlags,
        string source)
    {
        RecordActionCommand(
            agentId,
            0,
            agent,
            channel,
            targetActionIndex,
            startProgress,
            animationFlags,
            source);
    }

    public static void RecordMountActionCommand(
        Guid agentId,
        Agent mount,
        int channel,
        int targetActionIndex,
        float startProgress,
        AnimFlags animationFlags)
    {
        if (!animationTraceEnabled) return;
        mountActionCommands++;
        RecordActionCommand(
            agentId,
            0,
            mount,
            channel,
            targetActionIndex,
            startProgress,
            animationFlags,
            "mount-movement");
    }

    private static void RecordActionCommand(
        Guid agentId,
        long sequence,
        Agent agent,
        int channel,
        int targetActionIndex,
        float startProgress,
        AnimFlags animationFlags,
        string source)
    {
        if (!animationTraceEnabled) return;

        int currentActionIndex = agent.GetCurrentAction(channel).Index;
        float currentProgress = agent.GetCurrentActionProgress(channel);
        bool sameAction = currentActionIndex == targetActionIndex;
        bool restart = (animationFlags & AnimFlags.anf_restart) != 0;
        actionCommands++;
        if (restart) restartCommands++;
        if (restart && sameAction) sameActionRestarts++;
        if (sameAction
            && startProgress + ProgressTolerance < currentProgress)
        {
            rewindCommands++;
            if (rewindCommands <= MaximumRewindSamples)
            {
                AddEvent(
                    "animation-rewind",
                    agentId,
                    "observer",
                    source,
                    channel,
                    targetActionIndex,
                    agent.GetCurrentActionType(channel).ToString(),
                    startProgress,
                    sequence,
                    restart,
                    sameAction,
                    currentProgress: currentProgress);
            }
        }

        AddEvent(
            source + "-command",
            agentId,
            "observer",
            null,
            channel,
            targetActionIndex,
            agent.GetCurrentActionType(channel).ToString(),
            startProgress,
            sequence,
            restart,
            sameAction);
    }

    public static void RecordMountProgressReplay(
        Guid agentId,
        Agent mount,
        int channel,
        float targetProgress)
    {
        if (!animationTraceEnabled) return;
        mountProgressReplays++;
        float currentProgress = mount.GetCurrentActionProgress(channel);
        if (targetProgress + ProgressTolerance < currentProgress)
        {
            if (currentProgress > 0.9f && targetProgress < 0.1f)
                mountProgressWraps++;
            else
                mountProgressRewinds++;
        }
    }

    public static void RecordRetainedGuardReplay(string phase)
    {
        if (!animationTraceEnabled) return;
        if (phase == "before-native") beforeNativeGuardReplays++;
        else if (phase == "after-movement") afterMovementGuardReplays++;
        else if (phase == "after-native") afterNativeGuardReplays++;
    }

    public static void RecordGuardCommand(bool directionChanged)
    {
        if (!animationTraceEnabled) return;
        guardCommands++;
        if (directionChanged) guardDirectionCommands++;
    }

    public static void SampleAnimations(INetworkAgentRegistry registry)
    {
        if (!animationTraceEnabled || Mission.Current == null) return;

        SelectTraceAgents(registry);
        int generation = ++traceGeneration;
        long now = Stopwatch.GetTimestamp();
        foreach (string controllerId in registry.GetControllerIds())
        {
            foreach (CoopAgentInfo info in registry.GetAgents(controllerId))
            {
                Agent agent = info.Agent;
                if (agent == null
                    || agent.Mission != Mission.Current
                    || !agent.IsActive()
                    || !agent.IsHuman
                    || agent.IsMount)
                {
                    continue;
                }
                if (!TraceAgentIds.Contains(info.AgentId)
                    && !TrySelectActionTraceAgent(info.AgentId, agent))
                {
                    continue;
                }

                string role = registry.IsLocallyControlled(info.AgentId)
                    ? "owner"
                    : "observer";
                SampleAction(info.AgentId, role, agent, 0, generation, now);
                SampleAction(info.AgentId, role, agent, 1, generation, now);
                SampleMovement(info.AgentId, role, agent, generation, now);
            }
        }

        foreach (TraceKey key in Tracks
                     .Where(value => value.Value.Generation != generation)
                     .Select(value => value.Key)
                     .ToArray())
        {
            FinishTrack(key, now);
        }
    }

    private static void SelectTraceAgents(INetworkAgentRegistry registry)
    {
        if (traceAgentsSelected) return;

        // Keep the pipe payload bounded while sampling the same combatants on every peer.
        CoopAgentInfo[] candidates = registry.GetControllerIds()
            .SelectMany(controllerId => registry.GetAgents(controllerId))
            .Where(info =>
                info.Agent != null
                && info.Agent.Mission == Mission.Current
                && info.Agent.IsActive()
                && info.Agent.IsHuman
                && !info.Agent.IsMount)
            .GroupBy(info => info.AgentId)
            .Select(group => group.First())
            .ToArray();
        foreach (CoopAgentInfo info in candidates
                     .Where(value => value.Agent.HasMount)
                     .OrderBy(value => value.AgentId)
                     .Take(TraceAgentsPerMovementClass)
                     .Concat(candidates
                         .Where(value => !value.Agent.HasMount)
                         .OrderBy(value => value.AgentId)
                         .Take(TraceAgentsPerMovementClass)))
        {
            TraceAgentIds.Add(info.AgentId);
        }
        traceAgentsSelected = true;
    }

    private static bool TrySelectActionTraceAgent(Guid agentId, Agent agent)
    {
        bool selected = TrySelectActionTraceAgent(
            agentId,
            GetActionCategory(agent.GetCurrentActionType(0)));
        return TrySelectActionTraceAgent(
            agentId,
            GetActionCategory(agent.GetCurrentActionType(1))) || selected;
    }

    private static bool TrySelectActionTraceAgent(Guid agentId, string category)
    {
        HashSet<Guid> categoryAgentIds;
        if (category == "attack") categoryAgentIds = AttackTraceAgentIds;
        else if (category == "guard") categoryAgentIds = GuardTraceAgentIds;
        else if (category == "wield") categoryAgentIds = WieldTraceAgentIds;
        else return false;

        if (categoryAgentIds.Contains(agentId)) return true;
        if (categoryAgentIds.Count >= TraceAgentsPerActionClass) return false;
        categoryAgentIds.Add(agentId);
        TraceAgentIds.Add(agentId);
        return true;
    }

    private static void SampleAction(
        Guid agentId,
        string role,
        Agent agent,
        int channel,
        int generation,
        long now)
    {
        Agent.ActionCodeType actionType =
            agent.GetCurrentActionType(channel);
        string category = GetActionCategory(actionType);
        SampleTrack(
            new TraceKey(agentId, channel),
            role,
            category,
            agent.GetCurrentAction(channel).Index,
            actionType,
            agent.GetCurrentActionProgress(channel),
            generation,
            now);
    }

    private static void SampleMovement(
        Guid agentId,
        string role,
        Agent agent,
        int generation,
        long now)
    {
        bool mounted = agent.HasMount;
        Agent movementAgent = mounted ? agent.MountAgent : agent;
        if (movementAgent == null
            || !movementAgent.IsActive()
            || movementAgent.Mission != Mission.Current)
        {
            FinishTrack(new TraceKey(agentId, MovementChannel), now);
            return;
        }

        string category = movementAgent.GetCurrentVelocity().LengthSquared > 0.04f
            ? mounted
                ? "mounted-movement"
                : "on-foot-movement"
            : null;
        SampleTrack(
            new TraceKey(agentId, MovementChannel),
            role,
            category,
            movementAgent.GetCurrentAction(0).Index,
            movementAgent.GetCurrentActionType(0),
            movementAgent.GetCurrentActionProgress(0),
            generation,
            now);
    }

    private static void SampleTrack(
        TraceKey key,
        string role,
        string category,
        int actionIndex,
        Agent.ActionCodeType actionType,
        float progress,
        int generation,
        long now)
    {
        if (category == null)
        {
            FinishTrack(key, now);
            return;
        }

        Tracks.TryGetValue(key, out TraceTrack track);
        if (track != null
            && (track.Role != role
                || track.Category != category
                || track.ActionIndex != actionIndex))
        {
            FinishTrack(key, now);
            track = null;
        }
        if (track == null)
        {
            track = new TraceTrack
            {
                AgentId = key.AgentId,
                Role = role,
                Category = category,
                Channel = key.Channel,
                ActionIndex = actionIndex,
                ActionType = actionType,
                StartedAt = now,
                LastProgress = progress,
            };
            Tracks[key] = track;
            AddEvent(
                "animation-start",
                key.AgentId,
                role,
                category,
                key.Channel,
                actionIndex,
                actionType.ToString(),
                progress,
                0,
                false,
                false);
        }
        else if (progress + ProgressTolerance < track.LastProgress)
        {
            sampledProgressDrops++;
        }

        track.LastProgress = progress;
        track.Generation = generation;
    }

    private static void FinishTrack(TraceKey key, long now)
    {
        if (!Tracks.TryGetValue(key, out TraceTrack track)) return;
        Tracks.Remove(key);
        AddEvent(
            "animation-end",
            track.AgentId,
            track.Role,
            track.Category,
            track.Channel,
            track.ActionIndex,
            track.ActionType.ToString(),
            track.LastProgress,
            0,
            false,
            true,
            ToMilliseconds(now - track.StartedAt));
    }

    public static string SnapshotAnimationTrace(bool stop)
    {
        long now = Stopwatch.GetTimestamp();
        if (stop)
        {
            animationTraceEnabled = false;
            foreach (TraceKey key in Tracks.Keys.ToArray())
                FinishTrack(key, now);
        }

        return JsonConvert.SerializeObject(new
        {
            enabled = animationTraceEnabled,
            wallMilliseconds = ToMilliseconds(now - traceStartedAt),
            tracedAgentIds = TraceAgentIds
                .OrderBy(agentId => agentId)
                .Select(agentId => agentId.ToString("D"))
                .ToArray(),
            counters = new
            {
                outboundUpdates,
                remoteApplies,
                actionCommands,
                restartCommands,
                sameActionRestarts,
                rewindCommands,
                guardCommands,
                guardDirectionCommands,
                beforeNativeGuardReplays,
                afterMovementGuardReplays,
                afterNativeGuardReplays,
                mountActionCommands,
                mountProgressReplays,
                mountProgressWraps,
                mountProgressRewinds,
                sampledProgressDrops,
            },
            timelineTruncated = Timeline.Count >= MaximumTimelineEvents,
            timeline = Timeline,
        });
    }

    private static string GetActionCategory(
        Agent.ActionCodeType actionType)
    {
        if (actionType == Agent.ActionCodeType.EquipUnequip)
            return "wield";
        if (AgentActionData.IsDefendingAction(actionType)
            || AgentActionData.IsGuardReactionAction(actionType)
            || actionType == Agent.ActionCodeType.Guard)
        {
            return "guard";
        }
        if (actionType >=
            Agent.ActionCodeType.AttackMeleeAndRangedAllBegin
            && actionType <
            Agent.ActionCodeType.AttackMeleeAndRangedAllEnd)
        {
            return "attack";
        }
        return null;
    }

    private static void AddEvent(
        string kind,
        Guid agentId,
        string role,
        string category,
        int channel,
        int actionIndex,
        string actionType,
        float progress,
        long sequence,
        bool restart,
        bool sameAction,
        double durationMilliseconds = 0d,
        float currentProgress = 0f)
    {
        if (!kind.StartsWith("animation-", StringComparison.Ordinal)) return;
        if (agentId != Guid.Empty
            && !TraceAgentIds.Contains(agentId)) return;
        if (Timeline.Count >= MaximumTimelineEvents) return;
        Timeline.Add(new TraceEvent
        {
            Milliseconds = ToMilliseconds(
                Stopwatch.GetTimestamp() - traceStartedAt),
            DurationMilliseconds = durationMilliseconds,
            Kind = kind,
            AgentId = agentId == Guid.Empty
                ? null
                : agentId.ToString("D"),
            Role = role,
            Category = category,
            Channel = channel,
            ActionIndex = actionIndex,
            ActionType = actionType,
            Progress = progress,
            CurrentProgress = currentProgress,
            Sequence = sequence,
            Restart = restart,
            SameAction = sameAction,
        });
    }

    private static double ToMilliseconds(long ticks) =>
        1000d * ticks / Stopwatch.Frequency;
}
#endif
