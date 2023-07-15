using Common.Logging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Heroes.Interfaces;

internal interface IHeroInterface : IGameAbstraction
{
    byte[] PackageMainHero();
    bool TryResolveHero(string controllerId, out string heroId);
    void SwitchMainHero(string heroId);
    Hero UnpackMainHero(string controllerId, byte[] bytes);
}

internal class HeroInterface : IHeroInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();
    private readonly IObjectManager objectManager;
    private readonly IBinaryPackageFactory binaryPackageFactory;
    private readonly IHeroRegistry heroRegistry;

    public HeroInterface(
        IObjectManager objectManager,
        IBinaryPackageFactory binaryPackageFactory,
        IHeroRegistry heroRegistry)
    {
        this.objectManager = objectManager;
        this.binaryPackageFactory = binaryPackageFactory;
        this.heroRegistry = heroRegistry;
    }

    public byte[] PackageMainHero()
    {
        HeroBinaryPackage package = binaryPackageFactory.GetBinaryPackage<HeroBinaryPackage>(Hero.MainHero);
        return BinaryFormatterSerializer.Serialize(package);
    }

    public Hero UnpackMainHero(string controllerId, byte[] bytes)
    {
        HeroBinaryPackage package = BinaryFormatterSerializer.Deserialize<HeroBinaryPackage>(bytes);
        var hero = package.Unpack<Hero>(binaryPackageFactory);

        heroRegistry.TryRegisterHeroController(controllerId, hero.StringId);

        return hero;
    }

    public bool TryResolveHero(string controllerId, out string heroId) => heroRegistry.TryGetControlledHero(controllerId, out heroId);

    public void SwitchMainHero(string heroId)
    {
        if(objectManager.TryGetObject(heroId, out Hero resolvedHero))
        {
            Logger.Information("Switching to new hero: {heroName}", resolvedHero.Name.ToString());

            ChangePlayerCharacterAction.Apply(resolvedHero);
        }
        else
        {
            Logger.Warning("Could not find hero with id of: {guid}", heroId);
        }
    }
}
