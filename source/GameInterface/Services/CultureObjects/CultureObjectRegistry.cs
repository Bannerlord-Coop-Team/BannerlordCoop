using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects;
internal class CultureObjectRegistry : IAutoRegistry<CultureObject>
{
    ILogger Logger { get; }
    IObjectManager ObjectManager { get; }

    public CultureObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
    {
        Logger = logger;
        ObjectManager = objectManager;
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
        var networkId = $"{nameof(BasicCultureObject)}_{id}";
        ObjectManager.AddExisting<BasicCultureObject>(networkId, obj);
    }

    public void OnClientDestroyed(CultureObject obj, string id)
    {
        var networkId = $"{nameof(BasicCultureObject)}_{id}";

        if (ObjectManager.TryGetObject<BasicCultureObject>(networkId, out var resolvedObj) == false) return;

        ObjectManager.Remove(resolvedObj);
    }

    public void OnServerCreated(CultureObject obj, string id)
    {
        var networkId = $"{nameof(BasicCultureObject)}_{id}";
        ObjectManager.AddExisting<BasicCultureObject>(networkId, obj);
    }

    public void OnServerDestroyed(CultureObject obj, string id)
    {
        var networkId = $"{nameof(BasicCultureObject)}_{id}";

        if (ObjectManager.TryGetObject<BasicCultureObject>(networkId, out var resolvedObj) == false) return;

        ObjectManager.Remove(resolvedObj);
    }
}
