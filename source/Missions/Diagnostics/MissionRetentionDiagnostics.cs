#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using GameInterface;
using GameInterface.Services.ObjectManager;
using Missions.Agents;
using Missions.Services.Network;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace Missions.Diagnostics;

public interface IMissionRetentionDiagnostics
{
    void Track(object controller, IEnumerable<object> objects);
    object Read(long managedHeapBytes, bool fullCollection = false);
}

/// <summary>Session-owned weak observations, collected only by an explicit DEBUG command.</summary>
public sealed class MissionRetentionDiagnostics : IMissionRetentionDiagnostics
{
    private readonly Guid observationSession = Guid.NewGuid();
    private readonly List<Cycle> cycles = new();

    public void Track(object controller, IEnumerable<object> objects)
    {
        if (controller == null) throw new ArgumentNullException(nameof(controller));
        if (controller.GetType().IsValueType) throw new ArgumentException("A reference-type controller is required.", nameof(controller));
        var cycle = cycles.FirstOrDefault(x => ReferenceEquals(x.Controller.Target, controller));
        if (cycle == null)
        {
            if (cycles.Count >= 100) throw new InvalidOperationException("Diagnostic cycle limit reached; start a new test session.");
            cycle = new Cycle(controller);
            cycles.Add(cycle);
        }
        foreach (var value in objects.Append(controller).Where(x => x != null && !x.GetType().IsValueType))
        {
            if (cycle.Objects.Any(x => ReferenceEquals(x.Reference.Target, value))) continue;
            cycle.Objects.Add((value.GetType().FullName, new WeakReference(value)));
        }
    }

    public object Read(long managedHeapBytes, bool fullCollection = false) => new
    {
        observationSession,
        processId = Process.GetCurrentProcess().Id,
        processStartedUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
        sampledUtc = DateTime.UtcNow,
        managedHeapBytes,
        diagnosticFullCollection = fullCollection,
        observedMissionCount = cycles.Count,
        method = "weak references to observed controller, component, direct collaborators and snapshots",
        cycles = cycles.Select((cycle, index) => new
        {
            cycle = index + 1,
            missionType = cycle.Type,
            controllerAlive = cycle.Controller.IsAlive,
            types = cycle.Objects.GroupBy(x => x.Type).Select(group => new
            {
                type = group.Key,
                observed = group.Count(),
                alive = group.Count(x => x.Reference.IsAlive)
            }).ToArray()
        }).ToArray()
    };

    private sealed class Cycle
    {
        internal readonly string Type;
        internal readonly WeakReference Controller;
        internal readonly List<(string Type, WeakReference Reference)> Objects = new();
        internal Cycle(object controller)
        {
            Type = controller.GetType().FullName;
            Controller = new WeakReference(controller);
        }
    }
}

public sealed class MissionRetentionTrackCommand : ICoopCommand
{
    private readonly IMissionRetentionDiagnostics diagnostics;
    public MissionRetentionTrackCommand(IMissionRetentionDiagnostics diagnostics) => this.diagnostics = diagnostics;
    public string Prefix => "coop.debug.mission";
    public string Name => "retention_track";
    public string Description => "Weakly observes the current real mission graph before leaving it.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ModInformation.IsClient || args.Count != 0 || Mission.Current == null)
            return new CoopCommandResult(false, "A current client mission and no arguments are required.", "command_failed");
        var controller = Mission.Current.MissionBehaviors.OfType<CoopMissionController>().SingleOrDefault();
        if (controller == null)
            return new CoopCommandResult(false, "No co-op controller is attached to the native mission.", "command_failed");
        var objects = Fields(controller).Where(IsMissionObject).ToList();
        foreach (var component in objects.OfType<ICoopMissionComponent>().ToArray())
            objects.AddRange(Fields(component).Where(IsMissionObject));
        diagnostics.Track(controller, objects);
        return new CoopCommandResult(true, JsonConvert.SerializeObject(diagnostics.Read(GC.GetTotalMemory(false))));
    }

    // Reflect only our managed fields, never vanilla engine fields or native state.
    private static IEnumerable<object> Fields(object value)
    {
        for (var type = value.GetType(); type != null && type.Assembly == typeof(CoopMissionController).Assembly; type = type.BaseType)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                yield return field.GetValue(value);
    }

    private static bool IsMissionObject(object value) => value != null &&
        value is not IBattleNetwork && value is not IMissionContext &&
        value is not INetworkAgentRegistry && value is not INetworkWorldItemRegistry &&
        (value.GetType().Assembly == typeof(CoopMissionController).Assembly ||
         value.GetType().Namespace == "GameInterface.Services.Tournaments.Data");
}

public sealed class MissionRetentionCollectCommand : ICoopCommand
{
    private readonly IMissionRetentionDiagnostics diagnostics;
    private readonly IMessageBroker broker;
    private readonly Func<IBattleNetwork> network;
    private readonly IMissionContext context;
    private readonly IObjectManager objects;
    private readonly INetworkAgentRegistry agents;
    private readonly INetworkWorldItemRegistry items;

    public MissionRetentionCollectCommand(IMissionRetentionDiagnostics diagnostics, IMessageBroker broker,
        Func<IBattleNetwork> network, IMissionContext context, IObjectManager objects,
        INetworkAgentRegistry agents, INetworkWorldItemRegistry items)
    {
        this.diagnostics = diagnostics;
        this.broker = broker;
        this.network = network;
        this.context = context;
        this.objects = objects;
        this.agents = agents;
        this.items = items;
    }

    public string Prefix => "coop.debug.mission";
    public string Name => "retention_collect";
    public string Description => "Collects and measures ended mission graphs at campaign idle, for DEBUG diagnostics only.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ModInformation.IsClient || args.Count != 0 || Campaign.Current == null || Mission.Current != null)
            return new CoopCommandResult(false, "Client campaign idle without a mission and no arguments are required.", "command_failed");
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        return new CoopCommandResult(true, JsonConvert.SerializeObject(new
        {
            retention = diagnostics.Read(GC.GetTotalMemory(false), fullCollection: true),
            shared = new
            {
                broker = RuntimeHelpers.GetHashCode(broker),
                network = RuntimeHelpers.GetHashCode(network()),
                context = RuntimeHelpers.GetHashCode(context),
                objects = RuntimeHelpers.GetHashCode(objects),
                agents = RuntimeHelpers.GetHashCode(agents),
                items = RuntimeHelpers.GetHashCode(items)
            }
        }));
    }
}
#endif
