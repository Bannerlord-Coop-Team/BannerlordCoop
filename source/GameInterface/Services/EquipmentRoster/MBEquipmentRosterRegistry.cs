using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.EquipmentRoster;

internal class MBEquipmentRosterRegistry : AutoRegistryBase<MBEquipmentRoster>
{
    public MBEquipmentRosterRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(MBEquipmentRoster));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var equipmentRoster in MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>())
        {
            RegisterExistingObject(equipmentRoster.StringId, equipmentRoster);
        }
    }

    public override void OnClientCreated(MBEquipmentRoster obj, string id)
    {
    }

    public override void OnClientDestroyed(MBEquipmentRoster obj, string id)
    {
    }

    public override void OnServerCreated(MBEquipmentRoster obj, string id)
    {
    }

    public override void OnServerDestroyed(MBEquipmentRoster obj, string id)
    {
    }
}
