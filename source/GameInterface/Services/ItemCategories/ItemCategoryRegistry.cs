using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ItemCategorys;
internal class ItemCategoryRegistry : AutoRegistryBase<ItemCategory>
{
    public ItemCategoryRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(ItemCategory), new Type[] { typeof(string) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (ItemCategory item in MBObjectManager.Instance.GetObjectTypeList<ItemCategory>())
        {
            RegisterExistingObject(item.StringId, item);
        }
    }

    public override void OnClientCreated(ItemCategory obj, string id)
    {
    }

    public override void OnClientDestroyed(ItemCategory obj, string id)
    {
    }

    public override void OnServerCreated(ItemCategory obj, string id)
    {
    }

    public override void OnServerDestroyed(ItemCategory obj, string id)
    {
    }
}