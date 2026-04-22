using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager.Extensions;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.HeroDevelopers;

internal class HeroDeveloperRegistry : IAutoRegistry<HeroDeveloper>
{
    ILogger Logger { get; }
    public HeroDeveloperRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(HeroDeveloper));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<HeroDeveloper> registry)
    {
        foreach (var hero in Hero.AllAliveHeroes)
        {
            registry.RegisterExistingObject(hero.StringId, hero.HeroDeveloper);
        }

        foreach (var hero in Hero.DeadOrDisabledHeroes)
        {
            registry.RegisterExistingObject(hero.StringId, hero.HeroDeveloper);
        }
    }

    public void OnClientCreated(HeroDeveloper obj, string id)
    {
    }

    public void OnClientDestroyed(HeroDeveloper obj, string id)
    {
    }

    public void OnServerCreated(HeroDeveloper obj, string id)
    {
    }

    public void OnServerDestroyed(HeroDeveloper obj, string id)
    {
    }
}
