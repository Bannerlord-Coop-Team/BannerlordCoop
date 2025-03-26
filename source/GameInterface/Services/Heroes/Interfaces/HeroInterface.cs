using Common;
using Common.Logging;
using Common.Serialization;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Registry;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.ObjectSystem;

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
        objectManager.Remove(Hero.MainHero);
        objectManager.Remove(Hero.MainHero.PartyBelongedTo);
        objectManager.Remove(Hero.MainHero.Clan);
        objectManager.Remove(Hero.MainHero.CharacterObject);

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

        var heroId = RegisterObject(hero);
        
        entityRegistry.RegisterAsControlled(controllerId, heroId);

        return new Player()
        {
            HeroData = bytes,
            HeroStringId = heroId,
            PartyStringId = RegisterObject(hero.PartyBelongedTo),
            CharacterObjectStringId = RegisterObject(hero.CharacterObject),
            ClanStringId = RegisterObject(hero.Clan)
        };
    }

    private string RegisterObject<T>(T obj) where T : MBObjectBase
    {
        
        

        if (objectManager.AddNewObject(obj, out var newId) == false)
        {
            throw new InvalidOperationException($"Unable to register {obj.StringId} {typeof(T)}");
        }

        using (new AllowedThread())
        {
            obj.StringId = newId;
            MBObjectManager.Instance.RegisterObject(obj);
        }

        if (obj.StringId != newId) throw new Exception();

        return newId;
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

        // TODO ensure works
        var resolvedEntity = entities.SingleOrDefault(entity => entity.EntityId.StartsWith("hero"));

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
