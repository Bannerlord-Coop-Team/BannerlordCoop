using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings;

/// <summary>
/// Registry for <see cref="Building"/> type
/// </summary>
internal class BuildingRegistry : AutoRegistryBase<Building>
{
    public BuildingRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(Building));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var settlement in Settlement.All)
        {
            if (settlement.Town == null) continue;

            foreach (var building in settlement.Town.Buildings)
            {
                objectManager.AddExisting(settlement.StringId + "_" + building.BuildingType.ToString(), building);
            }
        }
    }

    public override void OnClientCreated(Building obj, string id)
    {
    }

    public override void OnClientDestroyed(Building obj, string id)
    {
    }

    public override void OnServerCreated(Building obj, string id)
    {
    }

    public override void OnServerDestroyed(Building obj, string id)
    {
    }
}
