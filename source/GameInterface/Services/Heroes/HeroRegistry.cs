﻿using Common;
using Common.Extensions;
using GameInterface.Services.ObjectManager.Extensions;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Registry;

/// <summary>
/// Registry for identifying ownership of <see cref="Hero"/> objects
/// </summary>
internal class HeroRegistry : RegistryBase<Hero>
{
    public static readonly string HeroStringIdPrefix = "CoopHero";

    public HeroRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;

        if (campaignObjectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        var heroes = campaignObjectManager.AliveHeroes.Concat(campaignObjectManager.DeadOrDisabledHeroes).ToArray();
        foreach (var hero in heroes)
        {
            base.RegisterExistingObject(hero.StringId, hero);
        }
    }

    public override bool RegisterExistingObject(string id, object obj)
    {
        var result = base.RegisterExistingObject(id, obj);

        AddToCampaignObjectManager(obj);

        return result;
    }

    protected override string GetNewId(Hero hero)
    {
        hero.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Hero>(HeroStringIdPrefix);
        return hero.StringId;
    }

    private void AddToCampaignObjectManager(object obj)
    {
        if (TryCast(obj, out var castedObj) == false) return;

        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null) return;

        objectManager.OnHeroAdded(castedObj);
    }
}
