using Common;
using Common.Extensions;
using Common.Logging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

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
        Hero.MainHero.StringId = string.Empty;
        Hero.MainHero.PartyBelongedTo.StringId = string.Empty;

        HeroBinaryPackage package = binaryPackageFactory.GetBinaryPackage<HeroBinaryPackage>(Hero.MainHero);
        return BinaryFormatterSerializer.Serialize(package);
    }

    public Hero UnpackMainHero(string controllerId, byte[] bytes)
    {
        Hero hero = null;

        GameLoopRunner.RunOnMainThread(() => {
            hero = UnpackMainHeroInternal(controllerId, bytes);
        },
        blocking: true);

        return hero;
    }

    private Hero UnpackMainHeroInternal(string controllerId, byte[] bytes)
    {
        HeroBinaryPackage package = BinaryFormatterSerializer.Deserialize<HeroBinaryPackage>(bytes);
        var hero = package.Unpack<Hero>(binaryPackageFactory);

        heroRegistry.TryRegisterHeroController(controllerId, hero.StringId);

        SetupNewHero(hero);

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

    private const string PlayerHeroStringIdPrefix = "PlayerHero";
    private const string PlayerPartyStringIdPrefix = "PlayerParty";
    private static readonly Action<CampaignObjectManager, Hero> CampaignObjectManager_AddHero = typeof(CampaignObjectManager)
        .GetMethod("AddHero", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildDelegate<Action<CampaignObjectManager, Hero>>();
    private static readonly Action<CampaignObjectManager, MobileParty> CampaignObjectManager_AddMobileParty = typeof(CampaignObjectManager)
        .GetMethod("AddMobileParty", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildDelegate<Action<CampaignObjectManager, MobileParty>>();
    private void SetupNewHero(Hero hero)
    {
        hero.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Hero>(PlayerHeroStringIdPrefix);
        hero.PartyBelongedTo.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<MobileParty>(PlayerPartyStringIdPrefix);

        SetupHeroWithObjectManagers(hero);
        SetupNewParty(hero);
    }

    private void SetupHeroWithObjectManagers(Hero hero)
    {
        objectManager.AddNewObject(hero, out string heroId);
        objectManager.AddNewObject(hero.PartyBelongedTo, out string _);

        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        if (campaignObjectManager == null)
        {
            Logger.Error("{type} was null when trying to register a {managedType}", typeof(CampaignObjectManager), typeof(Hero));
            return;
        }

        CampaignObjectManager_AddHero(campaignObjectManager, hero);
        CampaignObjectManager_AddMobileParty(campaignObjectManager, hero.PartyBelongedTo);
    }

    private void SetupNewParty(Hero hero)
    {
        hero.PartyBelongedTo.IsVisible = true;
        hero.PartyBelongedTo.Party.Visuals.SetMapIconAsDirty();
    }
}
