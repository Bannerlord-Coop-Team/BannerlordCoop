using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments;

/// <summary>
/// Registry for <see cref="Equipment"/> objects
/// </summary>
internal class EquipmentRegistry : IAutoRegistry<Equipment> {

    ILogger Logger { get; }
    public EquipmentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }
    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        // Not sure if this can be skipped since due constructor patching all equipment will already be registered.
        foreach (var equipmentRoster in Campaign.Current.AllEquipmentRosters)
        {
            if (equipmentRoster == null) continue;
            foreach (Equipment equipment in equipmentRoster.AllEquipments)
            {
                if (objectManager.Contains(equipment)) continue;

                objectManager.AddNewObject(equipment, out var _);
            }
        }
    }

    public void OnClientCreated(Equipment obj, string id)
    {
    }

    public void OnClientDestroyed(Equipment obj, string id)
    {
    }

    public void OnServerCreated(Equipment obj, string id)
    {
    }

    public void OnServerDestroyed(Equipment obj, string id)
    {
    }
}