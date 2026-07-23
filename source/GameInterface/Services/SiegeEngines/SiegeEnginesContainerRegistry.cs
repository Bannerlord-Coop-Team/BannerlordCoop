using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines;

internal class SiegeEnginesContainerRegistry : AutoRegistryBase<SiegeEnginesContainer>
{
    public SiegeEnginesContainerRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(SiegeEnginesContainer));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var siegeEvent in SiegeContainerLookup.ActiveSieges())
        {
            var settlementId = siegeEvent.BesiegedSettlement.StringId;
            foreach (var engineSide in SiegeContainerLookup.EngineContainers(siegeEvent))
            {
                RegisterExistingObject($"{settlementId}_siege_engines_{engineSide.Side}", engineSide.Container);
            }
        }
    }

    public override void OnClientCreated(SiegeEnginesContainer obj, string id)
    {
        // The interior is side-dependent and the side is unknown here; it is filled by
        // SiegeEnginesContainerShellPatches when the container is assigned to its owner.
    }

    public override void OnClientDestroyed(SiegeEnginesContainer obj, string id)
    {
    }

    public override void OnServerCreated(SiegeEnginesContainer obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeEnginesContainer obj, string id)
    {
    }
}
