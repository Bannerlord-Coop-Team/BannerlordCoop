using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.PropertyObjects;

internal class PropertyObjectRegistry : AutoRegistryBase<PropertyObject>
{
    public PropertyObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    // Needed for dictionaries that use PropertyObject as a key. Can't use surrogate because it won't be the same instance
    // If this is deleted, skills and other synced fields that use these as keys will stop working
    public override void RegisterAllObjects()
    {
    }

    public override void OnClientCreated(PropertyObject obj, string id)
    {
    }

    public override void OnClientDestroyed(PropertyObject obj, string id)
    {
    }

    public override void OnServerCreated(PropertyObject obj, string id)
    {
    }

    public override void OnServerDestroyed(PropertyObject obj, string id)
    {
    }
}
