using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ItemCategorys;
internal class ItemCategoryRegistry : IAutoRegistry<ItemCategory>
{
    ILogger Logger { get; }
    public ItemCategoryRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(ItemCategory), new Type[] { typeof(string) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<ItemCategory> registry)
    {
        foreach (ItemCategory item in MBObjectManager.Instance.GetObjectTypeList<ItemCategory>())
        {
            registry.RegisterExistingObject(item.StringId, item);
        }
    }

    public void OnClientCreated(ItemCategory obj, string id)
    {
    }

    public void OnClientDestroyed(ItemCategory obj, string id)
    {
    }

    public void OnServerCreated(ItemCategory obj, string id)
    {
    }

    public void OnServerDestroyed(ItemCategory obj, string id)
    {
    }
}