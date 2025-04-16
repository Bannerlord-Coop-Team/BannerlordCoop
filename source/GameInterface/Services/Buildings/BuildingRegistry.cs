using System;
using System.Collections.Generic;
using System.Reflection;
using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings;

/// <summary>
/// Registry for <see cref="Building"/> objects
/// </summary>
internal class BuildingRegistry : IAutoRegistry<Building>
{
    ILogger Logger { get; }

    public BuildingRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(Building), new Type[] { typeof(BuildingType), typeof(Town), typeof(float), typeof(int)} )
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<Building> registry)
    {
        foreach (Settlement settlement in Campaign.Current.Settlements)
        {
            if (settlement.Town == null) continue;

            foreach (Building building in settlement.Town.Buildings)
            {
                if (registry.RegisterNewObject(building, out var _) == false) Logger.Error($"Unable to register {nameof(Building)}");
            }
        }
    }

    public void OnClientCreated(Building obj, string id)
    {

    }

    public void OnClientDestroyed(Building obj, string id)
    {

    }

    public void OnServerCreated(Building obj, string id)
    {

    }

    public void OnServerDestroyed(Building obj, string id)
    {

    }
}


