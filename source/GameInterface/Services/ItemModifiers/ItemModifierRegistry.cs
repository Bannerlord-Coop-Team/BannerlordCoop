using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ItemModifiers;

/// <summary>
/// Registry for <see cref="ItemModifier"/> objects
/// </summary>
internal class ItemModifierRegistry : IAutoRegistry<ItemModifier>
{
    ILogger Logger { get; }

    private readonly IObjectManager objectManager;

    public ItemModifierRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;
        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(ItemModifier))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();
    
    public void RegisterAllObjects(IRegistry<ItemModifier> registry)
    {
        var objectManager = MBObjectManager.Instance;
        foreach (var group in objectManager.GetObjectTypeList<ItemModifierGroup>())
        {
            foreach(var itemModifier in group.ItemModifiers)
            {
                var networkId = itemModifier.StringId;
                registry.RegisterExistingObject(networkId, itemModifier);
            }
        }
    }

    public void OnClientCreated(ItemModifier obj, string id)
    {
    }

    public void OnClientDestroyed(ItemModifier obj, string id)
    {
    }

    public void OnServerCreated(ItemModifier obj, string id)
    {
    }

    public void OnServerDestroyed(ItemModifier obj, string id)
    {
    }
}
