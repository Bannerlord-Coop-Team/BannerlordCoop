using Common.Util;
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

namespace GameInterface.Services.HeroDevelopers;

internal class HeroDeveloperRegistry : AutoRegistryBase<HeroDeveloper>
{
    public HeroDeveloperRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(HeroDeveloper));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var hero in Hero.AllAliveHeroes)
        {
            RegisterExistingObject($"{hero.StringId}", hero.HeroDeveloper);
        }

        foreach (var hero in Hero.DeadOrDisabledHeroes)
        {
            RegisterExistingObject($"{hero.StringId}", hero.HeroDeveloper);
        }
    }

    public override void OnClientCreated(HeroDeveloper obj, string id)
    {
        using (new AllowedThread())
        {
            AccessTools.Field(typeof(HeroDeveloper), nameof(HeroDeveloper._skillXps)).SetValue(obj, new Dictionary<PropertyObject, float>());
            AccessTools.Field(typeof(HeroDeveloper), nameof(HeroDeveloper._newFocuses)).SetValue(obj, new PropertyOwner<SkillObject>());
        }
    }

    public override void OnClientDestroyed(HeroDeveloper obj, string id)
    {
    }

    public override void OnServerCreated(HeroDeveloper obj, string id)
    {
    }

    public override void OnServerDestroyed(HeroDeveloper obj, string id)
    {
    }
}
