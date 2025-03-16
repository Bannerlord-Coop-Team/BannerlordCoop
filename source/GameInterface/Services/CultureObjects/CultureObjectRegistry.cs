using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects;
internal class CultureObjectRegistry : IAutoRegistry<CultureObject>
{
    ILogger Logger { get; }
    public CultureObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(CultureObject))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<CultureObject> registry)
    {
        foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
        {
            registry.RegisterExistingObject(culture.StringId, culture);
        }
    }

    public void OnClientCreated(CultureObject obj, string id)
    {
    }

    public void OnClientDestroyed(CultureObject obj, string id)
    {
    }

    public void OnServerCreated(CultureObject obj, string id)
    {
    }

    public void OnServerDestroyed(CultureObject obj, string id)
    {
    }
}
