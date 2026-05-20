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
/// Registry for <see cref="ItemModifier"/> objects
/// </summary>
internal class ItemModifierRegistry : AutoRegistryBase<ItemModifier>
{
    public ItemModifierRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(ItemModifier))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();
    
    public override void RegisterAllObjects()
    {
        var mbObjectManager = MBObjectManager.Instance;
        foreach (var group in mbObjectManager.GetObjectTypeList<ItemModifierGroup>())
        {
            foreach(var itemModifier in group.ItemModifiers)
            {
                var networkId = itemModifier.StringId;
                RegisterExistingObject(networkId, itemModifier);
            }
        }
    }

    public override void OnClientCreated(ItemModifier obj, string id)
    {
    }

    public override void OnClientDestroyed(ItemModifier obj, string id)
    {
    }

    public override void OnServerCreated(ItemModifier obj, string id)
    {
    }

    public override void OnServerDestroyed(ItemModifier obj, string id)
    {
    }
}
