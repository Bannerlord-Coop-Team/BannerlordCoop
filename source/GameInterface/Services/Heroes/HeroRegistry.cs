using Common;
using System.Linq;
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
            RegisterExistingObject(hero.StringId, hero);
        }
    }

    protected override string GetNewId(Hero hero)
    {
        hero.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Hero>(HeroStringIdPrefix);
        return hero.StringId;
    }
}
