using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEventComponents.Patches;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;

/// <summary>
/// Registers <see cref="RaidEventComponent"/> lifetimes and queues its update patches.
/// </summary>
internal class RaidEventComponentRegistry : AutoRegistryBase<RaidEventComponent>
{
    public RaidEventComponentRegistry(
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager,
        IAutoSyncPatchCollector patchCollector)
        : base(logger, autoRegistryFactory, objectManager)
    {
        // Queued so Update is compiled after the AutoSync setter patches, otherwise it inlines the unpatched
        // SettlementHitPoints/Hearth setters and raid damage never syncs.
        var update = AccessTools.Method(typeof(RaidEventComponent), nameof(RaidEventComponent.Update));
        patchCollector.AddPrefix(update, AccessTools.Method(typeof(RaidEventComponentPatches), nameof(RaidEventComponentPatches.UpdatePrefix)));
        patchCollector.AddPostfix(update, AccessTools.Method(typeof(RaidEventComponentPatches), nameof(RaidEventComponentPatches.UpdatePostfix)));
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(RaidEventComponent), new Type[] { typeof(MapEvent) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(RaidEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(RaidEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(RaidEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(RaidEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
