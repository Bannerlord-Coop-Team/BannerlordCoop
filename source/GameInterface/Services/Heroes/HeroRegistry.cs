using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Registry;

/// <summary>
/// Registry for identifying ownership of <see cref="Hero"/> objects
/// </summary>
internal class HeroRegistry : RegistryBase<Hero>
{
    public static readonly string HeroStringIdPrefix = "CoopHero";
    private static int InstanceCounter = 0;

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
            RegisterNewObject(hero, out var _);
        }
    }

    protected override string GetNewId(Hero hero)
    {
        return $"{HeroStringIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
