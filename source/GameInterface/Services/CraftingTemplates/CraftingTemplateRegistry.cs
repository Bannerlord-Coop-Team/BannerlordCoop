using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CraftingTemplates;

internal class CraftingTemplateRegistry : IAutoRegistry<CraftingTemplate>
{
    ILogger Logger { get; }
    public CraftingTemplateRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<CraftingTemplate> registry)
    {
        foreach (CraftingTemplate skill in MBObjectManager.Instance.GetObjectTypeList<CraftingTemplate>())
        {
            registry.RegisterNewObject(skill, out _);
        }
    }

    public void OnClientCreated(CraftingTemplate obj, string id)
    {
    }

    public void OnClientDestroyed(CraftingTemplate obj, string id)
    {
    }

    public void OnServerCreated(CraftingTemplate obj, string id)
    {
    }

    public void OnServerDestroyed(CraftingTemplate obj, string id)
    {
    }
}