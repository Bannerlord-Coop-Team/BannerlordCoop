using Common;
using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.CharacterObjects;
internal class CharacterObjectRegistry : AutoRegistryBase<CharacterObject>
{
    public CharacterObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] { 
        AccessTools.Constructor(typeof(CharacterObject))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (CharacterObject character in CharacterObject.All)
        {
            RegisterExistingObject(character.StringId, character);
        }
    }

    public override void OnClientCreated(CharacterObject obj, string id)
    {
        using (new AllowedThread())
        {
            obj._characterTraits = new PropertyOwner<TraitObject>();
        }
    }

    public override void OnClientDestroyed(CharacterObject obj, string id)
    {
    }

    public override void OnServerCreated(CharacterObject obj, string id)
    {
    }

    public override void OnServerDestroyed(CharacterObject obj, string id)
    {
    }
}
