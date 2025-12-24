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
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes;

/// <summary>
/// Registry for identifying ownership of <see cref="Hero"/> objects
/// </summary>
internal class HeroRegistry : IAutoRegistry<Hero>
{
    ILogger Logger { get; }
    public HeroRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Hero), new Type[] { typeof(string) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<Hero> registry)
    {
        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;

        if (campaignObjectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var hero in campaignObjectManager.GetAllHeroes())
        {
            registry.RegisterExistingObject(hero.StringId, hero);
        }
    }

    public void OnClientCreated(Hero obj, string id)
    {
        using(new AllowedThread())
        {
            obj.Init();
            AccessTools.Field(typeof(Hero), nameof(Hero._children)).SetValue(obj, new MBList<Hero>());
            AccessTools.Field(typeof(Hero), nameof(Hero._ownedWorkshops)).SetValue(obj, new MBList<Workshop>());
        }

        MBObjectManager.Instance?.RegisterPresumedObject(obj);

        Campaign.Current?.CampaignObjectManager?.OnHeroAdded(obj);
    }

    public void OnClientDestroyed(Hero obj, string id)
    {
    }

    public void OnServerCreated(Hero obj, string id)
    {
    }

    public void OnServerDestroyed(Hero obj, string id)
    {
    }
}
