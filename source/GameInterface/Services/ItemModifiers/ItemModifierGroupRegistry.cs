using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ItemModifiers;

/// <summary>
/// Registry for <see cref="ItemModifierGroup"/> objects
/// </summary>
internal class ItemModifierGroupRegistry : AutoRegistryBase<ItemModifierGroup>
{
    public ItemModifierGroupRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(ItemModifierGroup))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();
    
    public override void RegisterAllObjects()
    {
        foreach (var itemModifierGroup in MBObjectManager.Instance.GetObjectTypeList<ItemModifierGroup>())
        {
            RegisterExistingObject(itemModifierGroup.StringId, itemModifierGroup);
        }
    }

    public override void OnClientCreated(ItemModifierGroup obj, string id)
    {
    }

    public override void OnClientDestroyed(ItemModifierGroup obj, string id)
    {
    }

    public override void OnServerCreated(ItemModifierGroup obj, string id)
    {
    }

    public override void OnServerDestroyed(ItemModifierGroup obj, string id)
    {
    }
}
