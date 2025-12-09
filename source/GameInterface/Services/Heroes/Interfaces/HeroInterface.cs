using Common;
using Common.Logging;
using Common.Serialization;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Registry;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

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
    
    private readonly IControlledEntityRegistry entityRegistry;

    public HeroInterface(
        IControlledEntityRegistry entityRegistry,
        IObjectManager objectManager)
    {
        this.objectManager = objectManager;
        this.entityRegistry = entityRegistry;
        this.objectManager = objectManager;
    }

    public byte[] PackageMainHero()
    {
        return Array.Empty<byte>();
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

        objectManager.TryGetId(hero, out var heroId);
        objectManager.TryGetId(hero.PartyBelongedTo, out var partyId);
        objectManager.TryGetId(hero.CharacterObject, out var characterObjectId);
        objectManager.TryGetId(hero.Clan, out var clanId);

        using (new AllowedThread())
        {
            hero.StringId = heroId;
            hero.PartyBelongedTo.StringId = partyId;
            hero.CharacterObject.StringId = characterObjectId;
            hero.Clan.StringId = clanId;
        }
        

        entityRegistry.RegisterAsControlled(controllerId, heroId);

        return new Player()
        {
            HeroData = bytes,
            HeroStringId = heroId,
            PartyStringId = partyId,
            CharacterObjectStringId = characterObjectId,
            ClanStringId = clanId
        };
    }

    private Hero UnpackMainHeroInternal(byte[] bytes)
    {
        return Hero.MainHero;
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

    private void SetupNewHero(Hero hero) { }

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

        partyBase.SetVisualAsDirty();
    }

    private void SetupNewParty(Hero hero)
    {
        var party = hero.PartyBelongedTo;
        party.IsVisible = true;
        party.Party.SetVisualAsDirty();
        CampaignEventDispatcher.Instance.OnPartyVisibilityChanged(party.Party);
    }
}
