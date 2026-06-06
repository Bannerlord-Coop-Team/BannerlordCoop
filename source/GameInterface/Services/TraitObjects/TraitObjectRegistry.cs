using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.TraitObjects;
internal class TraitObjectRegistry : AutoRegistryBase<TraitObject>
{
    public TraitObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(TraitObject), new Type[] { typeof(string) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (TraitObject trait in TraitObject.All)
        {
            RegisterExistingObject(trait.StringId, trait);
        }
    }

    public override void OnClientCreated(TraitObject obj, string id)
    {
    }

    public override void OnClientDestroyed(TraitObject obj, string id)
    {
    }

    public override void OnServerCreated(TraitObject obj, string id)
    {
    }

    public override void OnServerDestroyed(TraitObject obj, string id)
    {
    }
}