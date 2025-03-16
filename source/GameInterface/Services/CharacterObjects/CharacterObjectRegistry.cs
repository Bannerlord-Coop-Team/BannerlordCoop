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

namespace GameInterface.Services.CharacterObjects;
internal class CharacterObjectRegistry : IAutoRegistry<CharacterObject>
{
    ILogger Logger { get; }
    IObjectManager ObjectManager { get; }

    public CharacterObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
    {
        Logger = logger;
        ObjectManager = objectManager;
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
        var networkId = $"{nameof(BasicCharacterObject)}_{id}";
        ObjectManager.AddExisting<BasicCharacterObject>(networkId, obj);
    }

    public void OnClientDestroyed(CharacterObject obj, string id)
    {
        var networkId = $"{nameof(BasicCharacterObject)}_{id}";

        if (ObjectManager.TryGetObject<BasicCharacterObject>(networkId, out var resolvedObj) == false) return;

        ObjectManager.Remove(resolvedObj);
    }

    public void OnServerCreated(CharacterObject obj, string id)
    {
        var networkId = $"{nameof(BasicCharacterObject)}_{id}";
        ObjectManager.AddExisting<BasicCharacterObject>(networkId, obj);
    }

    public void OnServerDestroyed(CharacterObject obj, string id)
    {
        var networkId = $"{nameof(BasicCharacterObject)}_{id}";

        if (ObjectManager.TryGetObject<BasicCharacterObject>(networkId, out var resolvedObj) == false) return;

        ObjectManager.Remove(resolvedObj);
    }
}
