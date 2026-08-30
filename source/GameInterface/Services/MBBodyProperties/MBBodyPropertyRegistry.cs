using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MBBodyProperties;
internal class MBBodyPropertyRegistry : AutoRegistryBase<MBBodyProperty>
{
    public MBBodyPropertyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors =>
        AccessTools.GetDeclaredConstructors(typeof(MBBodyProperty));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var bodyProperty in MBObjectManager.Instance.GetObjectTypeList<MBBodyProperty>())
        {
            RegisterExistingObject(bodyProperty.StringId, bodyProperty);
        }
    }

    public override void OnClientCreated(MBBodyProperty obj, string id)
    {
    }

    public override void OnClientDestroyed(MBBodyProperty obj, string id)
    {
    }

    public override void OnServerCreated(MBBodyProperty obj, string id)
    {
    }

    public override void OnServerDestroyed(MBBodyProperty obj, string id)
    {
    }
}
