using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects;
internal class BasicCharacterObjectRegistry : IAutoRegistry<BasicCharacterObject>
{
    ILogger Logger { get; }
    public BasicCharacterObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(BasicCharacterObject))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<BasicCharacterObject> registry)
    {
        foreach (CharacterObject character in CharacterObject.All.OrderBy(c => c.Id))
        {
            registry.RegisterNewObject(character, out _);
        }
    }

    public void OnClientCreated(BasicCharacterObject obj, string id)
    {
    }

    public void OnClientDestroyed(BasicCharacterObject obj, string id)
    {
    }

    public void OnServerCreated(BasicCharacterObject obj, string id)
    {
    }

    public void OnServerDestroyed(BasicCharacterObject obj, string id)
    {
    }
}