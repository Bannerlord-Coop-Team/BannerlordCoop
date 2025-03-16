using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CharacterObjects;
internal class CharacterObjectRegistry : IAutoRegistry<CharacterObject>
{
    ILogger Logger { get; }
    public CharacterObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] { 
        AccessTools.Constructor(typeof(CharacterObject))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<CharacterObject> registry)
    {
        foreach (CharacterObject character in CharacterObject.All)
        {
            registry.RegisterExistingObject(character.StringId, character);
        }
    }

    public void OnClientCreated(CharacterObject obj, string id)
    {
    }

    public void OnClientDestroyed(CharacterObject obj, string id)
    {
    }

    public void OnServerCreated(CharacterObject obj, string id)
    {
    }

    public void OnServerDestroyed(CharacterObject obj, string id)
    {
    }
}
