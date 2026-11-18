using Common;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Rafts.Patches;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs;

/// <summary>
/// Registers every party's <see cref="MobilePartyAi"/> and queues the raft action patch behind its synced fields.
/// </summary>
internal class MobilePartyAiRegistry : AutoRegistryBase<MobilePartyAi>
{
    public MobilePartyAiRegistry(
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager,
        IAutoSyncPatchCollector patchCollector)
        : base(logger, autoRegistryFactory, objectManager)
    {
        if (!ModInformation.IsNavalEnabled) return;

        // Queued so ApplyInternal is compiled after the AutoSync _isDisabled patches, otherwise it inlines the unpatched
        // EnableAi/DisableAi and a raft's AI state never reaches clients.
        var applyInternal = AccessTools.Method(typeof(RaftStateChangeAction), nameof(RaftStateChangeAction.ApplyInternal));
        patchCollector.AddPrefix(applyInternal, AccessTools.Method(typeof(RaftStateChangeActionPatches), nameof(RaftStateChangeActionPatches.ApplyInternalPrefix)));
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var party in MobileParty.All)
        {
            if (party?.Ai == null) continue;
            RegisterExistingObject(party.StringId, party.Ai);
        }
    }

    public override void OnClientCreated(MobilePartyAi obj, string id)
    {
    }

    public override void OnClientDestroyed(MobilePartyAi obj, string id)
    {
    }

    public override void OnServerCreated(MobilePartyAi obj, string id)
    {
    }

    public override void OnServerDestroyed(MobilePartyAi obj, string id)
    {
    }
}
