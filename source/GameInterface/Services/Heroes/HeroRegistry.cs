using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Registry;

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
        AccessTools.Constructor(typeof(Hero), Array.Empty<Type>())
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (var hero in Hero.AllAliveHeroes)
        {
            objectManager.AddExisting(hero.StringId, hero);
        }

        foreach (var hero in Hero.DeadOrDisabledHeroes)
        {
            objectManager.AddExisting(hero.StringId, hero);
        }
    }

    public void OnClientCreated(Hero obj, string id)
    {
        using(new AllowedThread())
        {
            //obj.Init();
            AccessTools.Field(typeof(Hero), nameof(Hero._children)).SetValue(obj, new MBList<Hero>());
            AccessTools.Field(typeof(Hero), nameof(Hero._ownedWorkshops)).SetValue(obj, new MBList<Workshop>());
            AccessTools.Property(typeof(Hero), nameof(Hero.OwnedAlleys)).SetValue(obj, new MBList<Alley>());
            AccessTools.Property(typeof(Hero), nameof(Hero.OwnedCaravans)).SetValue(obj, new MBList<CaravanPartyComponent>());
            AccessTools.Field(typeof(Hero), nameof(Hero.VolunteerTypes)).SetValue(obj, new CharacterObject[6]);
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
