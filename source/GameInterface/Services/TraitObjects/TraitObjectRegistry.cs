using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.TraitObjects;
internal class TraitObjectRegistry : IAutoRegistry<TraitObject>
{
    ILogger Logger { get; }
    public TraitObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(TraitObject), new Type[] { typeof(string) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<TraitObject> registry)
    {
        foreach (TraitObject trait in TraitObject.All)
        {
            registry.RegisterExistingObject(trait.StringId, trait);
        }
    }

    public void OnClientCreated(TraitObject obj, string id)
    {
    }

    public void OnClientDestroyed(TraitObject obj, string id)
    {
    }

    public void OnServerCreated(TraitObject obj, string id)
    {
    }

    public void OnServerDestroyed(TraitObject obj, string id)
    {
    }
}