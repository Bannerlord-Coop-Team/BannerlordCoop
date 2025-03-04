using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.BasicCultureObjects;
internal class BasicCultureObjectRegistry : IAutoRegistry<BasicCultureObject>
{
    ILogger Logger { get; }
    public BasicCultureObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(BasicCultureObject))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<BasicCultureObject> registry)
    {
        foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
        {
            registry.RegisterNewObject(culture, out _);
        }
    }

    public void OnClientCreated(BasicCultureObject obj, string id)
    {
    }

    public void OnClientDestroyed(BasicCultureObject obj, string id)
    {
    }

    public void OnServerCreated(BasicCultureObject obj, string id)
    {
    }

    public void OnServerDestroyed(BasicCultureObject obj, string id)
    {
    }
}
