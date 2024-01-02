using Common;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Registry;


/// <summary>
/// Registry for identifying ownership of <see cref="Hero"/> objects
/// </summary>
internal interface IHeroRegistry : IRegistry<Hero>
{
    void RegisterAllHeroes();
    bool RegisterHero(Hero hero);
    bool RemoveHero(Hero hero);
}

/// <inheritdoc cref="IHeroRegistry"/>
internal class HeroRegistry : RegistryBase<Hero>, IHeroRegistry
{
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
            RegisterHero(hero);
        }
    }

    public bool RegisterHero(Hero hero)
    {
        if (RegisterExistingObject(hero.StringId, hero) == false)
        {
            Logger.Warning("Unable to register hero: {object}", hero.Name);
            return false;
        }

        return true;
    }

    public bool RemoveHero(Hero hero) => Remove(hero.StringId);


    public static readonly string HeroStringIdPrefix = "CoopHero";
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
