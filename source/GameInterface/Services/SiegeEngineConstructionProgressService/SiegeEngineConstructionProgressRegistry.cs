using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

using GameInterface.Services.SiegeEngines;
namespace GameInterface.Services.SiegeEngineConstructionProgressService;

internal class SiegeEngineConstructionProgressRegistry : AutoRegistryBase<SiegeEngineConstructionProgress>
{
    public SiegeEngineConstructionProgressRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(SiegeEngineConstructionProgress));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var siegeEvent in SiegeContainerLookup.ActiveSieges())
        {
            var settlementId = siegeEvent.BesiegedSettlement.StringId;
            foreach (var engineSide in SiegeContainerLookup.EngineContainers(siegeEvent))
            {
                RegisterSide(settlementId, engineSide.Side, engineSide.Container);
            }
        }
    }

    // Save order is stable across machines, so list indexes give the joiner the same derived ids the
    // server computes. Removed engines are included because a redeploy can bring them back.
    private void RegisterSide(string settlementId, string side, SiegeEnginesContainer container)
    {
        if (container == null) return;

        if (container.SiegePreparations != null)
        {
            RegisterExistingObject($"{settlementId}_prep_{side}", container.SiegePreparations);
        }

        for (int i = 0; i < container.DeployedSiegeEngines.Count; i++)
        {
            RegisterExistingObject($"{settlementId}_deployed_{side}_{i}", container.DeployedSiegeEngines[i]);
        }

        for (int i = 0; i < container.ReservedSiegeEngines.Count; i++)
        {
            RegisterExistingObject($"{settlementId}_reserved_{side}_{i}", container.ReservedSiegeEngines[i]);
        }

        for (int i = 0; i < container.RemovedSiegeEngines.Count; i++)
        {
            var removed = container.RemovedSiegeEngines[i]?.SiegeEngine;
            if (removed == null) continue;

            RegisterExistingObject($"{settlementId}_removed_{side}_{i}", removed);
        }
    }

    public override void OnClientCreated(SiegeEngineConstructionProgress obj, string id)
    {
        // Every vanilla constructor sets RedeploymentProgress to 1f; the shell default of 0f would
        // leave IsBeingRedeployed true and the engine permanently inactive on this client.
        obj.RedeploymentProgress = 1f;
    }

    public override void OnClientDestroyed(SiegeEngineConstructionProgress obj, string id)
    {
    }

    public override void OnServerCreated(SiegeEngineConstructionProgress obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeEngineConstructionProgress obj, string id)
    {
    }
}