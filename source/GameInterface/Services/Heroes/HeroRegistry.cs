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
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Registry;

/// <summary>
/// Registry for identifying ownership of <see cref="Hero"/> objects
/// </summary>
internal class HeroRegistry : AutoRegistryBase<Hero>
{
    public HeroRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager) : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Hero), Array.Empty<Type>())
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var hero in Hero.AllAliveHeroes)
        {
            RegisterExistingObject(hero.StringId, hero);
        }

        foreach (var hero in Hero.DeadOrDisabledHeroes)
        {
            RegisterExistingObject(hero.StringId, hero);
        }
    }

    public override void OnClientCreated(Hero obj, string id)
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

    public override void OnClientDestroyed(Hero obj, string id)
    {
    }

    public override void OnServerCreated(Hero obj, string id)
    {
    }

    public override void OnServerDestroyed(Hero obj, string id)
    {
    }
}
