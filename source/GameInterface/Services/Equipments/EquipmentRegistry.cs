using Common.Util;
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
    private static readonly ConstructorInfo EquipmentCtor = AccessTools.Constructor(typeof(Equipment));

    public EquipmentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    // Only the parameterless and copy constructors are hooked. Equipment(EquipmentType)
    // chains to the parameterless ctor via ': this()', so it is covered through that
    // hook; registering it as well would publish the creation twice.
    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(Equipment)),
        AccessTools.Constructor(typeof(Equipment), new[] { typeof(Equipment) }),
    };

    // Equipment removal is driven by Hero lifecycle (Hero.OnDeath / Hero.ResetEquipments,
    // which remove both the battle and civilian equipment), not by an Equipment method,
    // so it cannot be expressed as a per-instance DestroyMethod. See EquipmentLifetimePatches.
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
        // Client instances are created via SkipConstructor, so run the default ctor
        // (under AllowedThread so the lifetime prefix treats it as an allowed original
        // and does not re-publish) to initialize the _itemSlots backing array the same
        // way the parameterless constructor would. Item-slot sync requires a valid array.
        using (new AllowedThread())
        {
            EquipmentCtor.Invoke(obj, Array.Empty<object>());
        }
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