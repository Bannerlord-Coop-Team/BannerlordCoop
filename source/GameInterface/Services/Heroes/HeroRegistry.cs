using Common;
using ProtoBuf;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Registry;

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
        if (objIds.TryGetValue(heroId, out var hero) == false) return false;

        return IsControlled(hero);
    }

    public bool IsControlled(Hero hero)
    {
        return heroControllerExtension.TryGetValue(hero, out _);
    }

    public bool IsControlledBy(string controllerId, string heroId)
    {
        if (objIds.TryGetValue(heroId, out var hero) == false) return false;

        return IsControlledBy(controllerId, hero);
    }

    public bool IsControlledBy(string controllerId, Hero hero)
    {
        if (heroControllerExtension.TryGetValue(hero, out var resolvedControllerId) == false) return false;

        return resolvedControllerId == controllerId;
    }
}
