using Common;
using Common.Extensions;
using Common.Logging;
using Common.Serialization;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.Clans;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PartyVisuals.Extensions;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Registry;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Interfaces;

public interface IHeroInterface : IGameAbstraction
{
    byte[] PackageMainHero();
    bool TryResolveHero(string controllerId, out string heroId);
    void SwitchMainHero(string heroId);
    /// <summary>
    /// Unpacks and properly constructs hero in the game
    /// </summary>
    /// <param name="bytes">Hero as bytes</param>
    /// <returns>Hero string identifier</returns>
    Player UnpackHero(string controllerId, byte[] bytes);
}

internal class HeroInterface : IHeroInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();
    private readonly IObjectManager objectManager;
    private readonly IBinaryPackageFactory binaryPackageFactory;
    private readonly IControlledEntityRegistry entityRegistry;



    public HeroInterface(
        IBinaryPackageFactory binaryPackageFactory,
        IControlledEntityRegistry entityRegistry,
        IObjectManager objectManager)
    {
        this.objectManager = objectManager;
        this.binaryPackageFactory = binaryPackageFactory;
        this.entityRegistry = entityRegistry;
        this.objectManager = objectManager;
    }

    public byte[] PackageMainHero()
    {
        Hero.MainHero.StringId = string.Empty;
        Hero.MainHero.PartyBelongedTo.StringId = string.Empty;
        Hero.MainHero.Clan.StringId = string.Empty;
        Hero.MainHero.CharacterObject.StringId = string.Empty;

        HeroBinaryPackage package = binaryPackageFactory.GetBinaryPackage<HeroBinaryPackage>(Hero.MainHero);

        return BinaryFormatterSerializer.Serialize(package);
    }

    public Player UnpackHero(string controllerId, byte[] bytes)
    {
        Hero hero = null;

        GameLoopRunner.RunOnMainThread(() => {
            using(new AllowedThread())
            {
                hero = UnpackMainHeroInternal(bytes);
            }
        },
        blocking: true);

        entityRegistry.RegisterAsControlled(controllerId, hero.StringId);

        var playerData = new Player(
            bytes,
            hero.StringId,
            hero.PartyBelongedTo.StringId,
            hero.CharacterObject.StringId,
            hero.Clan.StringId
        );

        return playerData;
    }

    private Hero UnpackMainHeroInternal(byte[] bytes)
    {
        HeroBinaryPackage package = BinaryFormatterSerializer.Deserialize<HeroBinaryPackage>(bytes);
        var hero = package.Unpack<Hero>(binaryPackageFactory);

        SetupNewHero(hero);

        return hero;
    }

    public bool TryResolveHero(string controllerId, out string heroId)
    {
        heroId = null;

        if (entityRegistry.TryGetControlledEntities(controllerId, out var entities) == false)
        {
            Logger.Error("Unable to resolve hero for {controllerId}", controllerId);
            return false;
        }

        var resolvedEntity = entities.SingleOrDefault(entity => entity.EntityId.StartsWith(HeroRegistry.HeroStringIdPrefix));

        if (resolvedEntity == null)
        {
            Logger.Error("No hero was registered for {controllerId}", controllerId);
            return false;
        }

        heroId = resolvedEntity.EntityId;

        return true;
    }

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

    private void SetupNewHero(Hero hero)
    {
        SetupHeroWithObjectManagers(hero);
        SetupNewParty(hero);
    }

    private void SetupHeroWithObjectManagers(Hero hero)
    {
        objectManager.AddNewObject(hero, out var _);
        objectManager.AddNewObject(hero.PartyBelongedTo, out var _);
        objectManager.AddNewObject(hero.Clan, out var _);

        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        if (campaignObjectManager == null)
        {
            Logger.Error("{type} was null when trying to register a {managedType}", typeof(CampaignObjectManager), typeof(Hero));
            return;
        }

        campaignObjectManager.AddHero(hero);

        var party = hero.PartyBelongedTo;

        campaignObjectManager.AddMobileParty(party);

        var partyBase = party.Party;

        campaignObjectManager.AddClan(hero.Clan);

        partyBase.GetPartyVisual().OnStartup();
        partyBase.SetVisualAsDirty();
    }

    private void SetupNewParty(Hero hero)
    {
        var party = hero.PartyBelongedTo;
        party.IsVisible = true;
        party.Party.SetVisualAsDirty();

        party.RecoverPositionsForNavMeshUpdate();
        party.CurrentNavigationFace = Campaign.Current.MapSceneWrapper.GetFaceIndex(party.Position2D);

        party.Ai.OnGameInitialized();

        CampaignEventDispatcher.Instance.OnPartyVisibilityChanged(party.Party);
    }
}
