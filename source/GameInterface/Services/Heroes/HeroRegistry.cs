using Common;
using Common.Extensions;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Registry;


/// <summary>
/// Registry for identifying ownership of <see cref="Hero"/> objects
/// </summary>
internal interface IHeroRegistry : IRegistry<Hero>
{
    void RegisterAllHeroes();
    bool TryGetControlledHero(string controllerId, out string heroId);
    bool TryRegisterHeroController(string controllerId, string heroId);
    bool TryRemoveHeroController(string controllerId, string heroId);

    bool IsControlled(string heroId);
    bool IsControlled(Hero hero);
    bool IsControlledBy(string controllerId, string heroId);
    bool IsControlledBy(string controllerId, Hero hero);
}

/// <inheritdoc cref="IHeroRegistry"/>
[ProtoContract]
internal class HeroRegistry : RegistryBase<Hero>, IHeroRegistry
{
    [ProtoMember(1)]
    private readonly ConcurrentDictionary<string, string> controlledHeros = new ConcurrentDictionary<string, string>();

    private static readonly ConditionalWeakTable<Hero, string> heroControllerExtension = new ConditionalWeakTable<Hero, string>();

    public void RegisterAllHeroes()
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
            if (RegisterExistingObject(hero.StringId, hero) == false)
            {
                Logger.Warning("Unable to register hero: {object}", hero.Name);
            }
        }
    }

    public bool TryRegisterHeroController(string controllerId, string heroId)
    {
        // Input validation
        if (string.IsNullOrEmpty(controllerId)) return false;
        if (string.IsNullOrEmpty(heroId)) return false;

        if (controlledHeros.ContainsKey(controllerId))
        {
            Logger.Error("Tried to register {controller} with hero {heroId}, but they were already registered", controllerId, heroId);

            return false;
        }


        if (objIds.TryGetValue(heroId, out var hero) == false) return false;

        // If hero already registered as controlled return false
        if (heroControllerExtension.TryGetValue(hero, out _))
        {
            Logger.Error("Found controller id registered with hero but did not exist in {controlledHeroes}", nameof(controlledHeros));
            return false;
        }

        var result = true;

        result &= controlledHeros.TryAdd(controllerId, heroId);
        heroControllerExtension.Add(hero, controllerId);

        return result;
    }

    public bool TryRemoveHeroController(string controllerId, string heroId)
    {
        // Input validation
        if (string.IsNullOrEmpty(controllerId)) return false;
        if (string.IsNullOrEmpty(heroId)) return false;

        if (controlledHeros.ContainsKey(controllerId) == false) return false;

        if (objIds.TryGetValue(heroId, out var hero) == false) return false;

        // If hero controller id was not set with hero then log error
        if (heroControllerExtension.TryGetValue(hero, out _) == false)
        {
            Logger.Error("Found controller id registered with {controlledHeros} but did not exist in {hero}", nameof(controlledHeros), nameof(Hero));

            return false;
        }

        var result = true;

        result &= controlledHeros.TryRemove(controllerId, out _);
        result &= heroControllerExtension.Remove(hero);

        return result;
    }

    public bool TryGetControlledHero(string controllerId, out string heroId) => controlledHeros.TryGetValue(controllerId, out heroId);

    public bool IsControlled(string heroId)
    {
        // Input validation
        if (string.IsNullOrEmpty(heroId)) return false;

        if (objIds.TryGetValue(heroId, out var hero) == false) return false;

        return IsControlled(hero);
    }

    public bool IsControlled(Hero hero)
    {
        // Input validation
        if (hero == null) return false;

        return heroControllerExtension.TryGetValue(hero, out _);
    }

    public bool IsControlledBy(string controllerId, string heroId)
    {
        // Input validation
        if (string.IsNullOrEmpty(controllerId)) return false;
        if (string.IsNullOrEmpty(heroId)) return false;

        if (objIds.TryGetValue(heroId, out var hero) == false) return false;

        return IsControlledBy(controllerId, hero);
    }

    public bool IsControlledBy(string controllerId, Hero hero)
    {
        // Input validation
        if (string.IsNullOrEmpty(controllerId)) return false;
        if (hero == null) return false;

        if (heroControllerExtension.TryGetValue(hero, out var resolvedControllerId) == false) return false;

        return resolvedControllerId == controllerId;
    }

    private const string HeroStringIdPrefix = "CoopHero";
    public override bool RegisterNewObject(Hero obj, out string id)
    {
        id = null;

        // Input validation
        if (obj == null) return false;

        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;

        if (campaignObjectManager == null) return false;

        var newId = campaignObjectManager.FindNextUniqueStringId<Hero>(HeroStringIdPrefix);

        if (objIds.ContainsKey(newId)) return false;

        obj.StringId = newId;

        objIds.Add(newId, obj);

        id = newId;

        return true;
    }
}
