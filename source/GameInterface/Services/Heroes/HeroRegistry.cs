using Common;
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
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
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

    // A client's instance is allocated by FormatterServices.GetUninitializedObject, so nothing the constructor
    // assigns exists on it. Run the real constructor instead of restating its assignments here: the hand-written
    // version of this drifted from it and left Hero._exSpouses null, which took the clan screen down (#2551).
    // Hero's constructor only assigns fields - it registers nothing and raises no events - so running it on an
    // already-allocated instance leaves exactly the state a constructed hero has.
    private static readonly ConstructorInfo HeroConstructor = AccessTools.Constructor(typeof(Hero));

    public override void OnClientCreated(Hero obj, string id)
    {
        using(new AllowedThread())
        {
            HeroConstructor.Invoke(obj, null);
        }

        GameThread.RunSafe(() =>
        {
            Campaign.Current?.CampaignObjectManager?.AddHero(obj);

            MBObjectManager.Instance?.RegisterObjectInternalWithoutTypeId(obj, presumed: false, out _);
        });
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
