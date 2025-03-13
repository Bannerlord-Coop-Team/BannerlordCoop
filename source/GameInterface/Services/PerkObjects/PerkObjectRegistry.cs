using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.PerkObjects;
internal class ItemCategoryRegistry : IAutoRegistry<PerkObject>
{
    ILogger Logger { get; }
    public ItemCategoryRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(PerkObject), new Type[] { typeof(string) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<PerkObject> registry)
    {
        foreach (PerkObject trait in PerkObject.All)
        {
            registry.RegisterNewObject(trait, out _);
        }
    }

    public void OnClientCreated(PerkObject obj, string id)
    {
    }

    public void OnClientDestroyed(PerkObject obj, string id)
    {
    }

    public void OnServerCreated(PerkObject obj, string id)
    {
    }

    public void OnServerDestroyed(PerkObject obj, string id)
    {
    }
}