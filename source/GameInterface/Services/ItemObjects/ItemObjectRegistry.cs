using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ItemObjects;

public class ItemObjectRegistry : IAutoRegistry<ItemObject>
{
    ILogger Logger { get; }
    public ItemObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(ItemObject));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<ItemObject> registry)
    {
        // Must order by string id as this is not deterministic on load
        foreach (ItemObject Item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>().OrderBy(i => i.StringId))
        {
            registry.RegisterExistingObject(Item.StringId, Item);
        }
    }

    public void OnClientCreated(ItemObject obj, string id)
    {
    }

    public void OnClientDestroyed(ItemObject obj, string id)
    {
    }

    public void OnServerCreated(ItemObject obj, string id)
    {
    }

    public void OnServerDestroyed(ItemObject obj, string id)
    {
    }
}
