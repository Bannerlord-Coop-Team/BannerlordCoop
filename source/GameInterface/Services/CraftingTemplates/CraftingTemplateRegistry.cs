using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CraftingTemplates;

internal class CraftingTemplateRegistry : AutoRegistryBase<CraftingTemplate>
{
    public CraftingTemplateRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var obj in MBObjectManager.Instance.GetObjectTypeList<CraftingTemplate>())
        {
            RegisterExistingObject(obj.StringId, obj);
        }
    }

    public override void OnClientCreated(CraftingTemplate obj, string id)
    {
    }

    public override void OnClientDestroyed(CraftingTemplate obj, string id)
    {
    }

    public override void OnServerCreated(CraftingTemplate obj, string id)
    {
    }

    public override void OnServerDestroyed(CraftingTemplate obj, string id)
    {
    }
}