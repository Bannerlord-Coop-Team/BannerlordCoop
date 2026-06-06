using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments;

/// <summary>
/// Registry for <see cref="Equipment"/> objects
/// </summary>
internal class EquipmentRegistry : AutoRegistryBase<Equipment>
{
    public EquipmentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var equipmentRosters = Campaign.Current?.AllEquipmentRosters?.Where(roster => roster != null);
        if (equipmentRosters == null) return;

        foreach (var roster in equipmentRosters)
        {
            var equipments = roster.AllEquipments?.Where(equipment => equipment != null);
            if (equipments == null) continue;

            var i = 0;
            foreach (var equipment in equipments)
            {
                var id = $"{roster.StringId}_{i++}";
                RegisterExistingObject(id, equipment);
            }
        }
    }

    public override void OnClientCreated(Equipment obj, string id)
    {
    }

    public override void OnClientDestroyed(Equipment obj, string id)
    {
    }

    public override void OnServerCreated(Equipment obj, string id)
    {
    }

    public override void OnServerDestroyed(Equipment obj, string id)
    {
    }
}