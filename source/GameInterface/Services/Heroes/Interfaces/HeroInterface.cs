using Common;
using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players.Data;
using SandBox.View.Map.Managers;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes.Interfaces;

public interface IHeroInterface : IGameAbstraction
{
    byte[] PackageMainHero();
    bool TryResolve<T>(string controllerId, out string heroId);
    void SwitchToPlayer(Player player);
    /// <summary>
    /// Unpacks and properly constructs hero in the game
    /// </summary>
    /// <param name="bytes">Hero as bytes</param>
    /// <returns>Hero string identifier</returns>
    NetworkPlayerData UnpackHero(string controllerId, byte[] bytes);
}

internal class HeroInterface : IHeroInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly IBinaryPackageFactory binaryPackageFactory;
    private readonly IControlledEntityRegistry entityRegistry;

    public HeroInterface(
        IMessageBroker messageBroker,
        IBinaryPackageFactory binaryPackageFactory,
        IControlledEntityRegistry entityRegistry,
        IObjectManager objectManager)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
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

    public NetworkPlayerData UnpackHero(string controllerId, byte[] bytes)
    {
        Hero hero = null;

        GameLoopRunner.RunOnMainThread(() => {
            using(new AllowedThread())
            {
                hero = UnpackMainHeroInternal(bytes);
            }
        },
        blocking: true);

        // Retrieve the Coop-assigned IDs so they can be written back onto each object's
        // StringId. If null (due to an ID collision fixed in AutoRegistry.RegisterExistingObject),
        // log and skip — assigning null would corrupt the object in CampaignObjectManager.
        if (!objectManager.TryGetId(hero, out var heroId))
            Logger.Error("Failed to retrieve coop ID for hero, StringId will not be updated");
        if (!objectManager.TryGetId(hero.PartyBelongedTo, out var partyId))
            Logger.Error("Failed to retrieve coop ID for hero's party (StringId={ExistingId}), StringId will not be updated", hero.PartyBelongedTo?.StringId);
        if (!objectManager.TryGetId(hero.CharacterObject, out var characterObjectId))
            Logger.Error("Failed to retrieve coop ID for hero's CharacterObject, StringId will not be updated");
        if (!objectManager.TryGetId(hero.Clan, out var clanId))
            Logger.Error("Failed to retrieve coop ID for hero's Clan, StringId will not be updated");

        //using (new AllowedThread())
        //{
        //    // Guard against null — original code assigned unconditionally which would
        //    // leave objects with vanilla StringIds (e.g. "main_hero") and crash on load.
        //    if (heroId != null) hero.StringId = heroId;
        //    if (partyId != null) hero.PartyBelongedTo.StringId = partyId;
        //    if (characterObjectId != null) hero.CharacterObject.StringId = characterObjectId;
        //    if (clanId != null) hero.Clan.StringId = clanId;
        //}

        entityRegistry.RegisterAsControlled(controllerId, heroId);
        entityRegistry.RegisterAsControlled(controllerId, partyId);
        entityRegistry.RegisterAsControlled(controllerId, characterObjectId);
        entityRegistry.RegisterAsControlled(controllerId, clanId);

        return new NetworkPlayerData()
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
        HeroBinaryPackage package = BinaryFormatterSerializer.Deserialize<HeroBinaryPackage>(bytes);
        var hero = package.Unpack<Hero>(binaryPackageFactory);

        SetupNewHero(hero);

        return hero;
    }

    public bool TryResolve<T>(string controllerId, out string controlledObjectId)
    {
        controlledObjectId = null;

        if (entityRegistry.TryGetControlledEntities(controllerId, out var entities) == false)
        {
            Logger.Warning("Unable to resolve hero for {controllerId}", controllerId);
            return false;
        }

        var heroEntities = entities.Where(entity => objectManager.TryGetObject<T>(entity.EntityId, out _)).ToList();

        if (heroEntities.Count == 0)
        {
            Logger.Information("No hero was registered for {controllerId}", controllerId);
            return false;
        }

        if (heroEntities.Count > 1)
        {
            Logger.Warning("Multiple heroes registered for {controllerId}, using first match", controllerId);
        }

        var resolvedEntity = heroEntities[0];

        controlledObjectId = resolvedEntity.EntityId;

        return true;
    }

    public void SwitchToPlayer(Player player)
    {
        if (!objectManager.TryGetObjectWithLogging(player.HeroId, out Hero playerHero))
            return;
        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out MobileParty playerParty))
            return;

        Campaign.Current.MainParty = playerParty;
        // This is needed because if the player is captured the PartyBelongedTo is null
        playerHero.PartyBelongedTo = playerParty;

        Logger.Information("Switching to new hero: {heroName}", playerHero.Name.ToString());

        ChangePlayerCharacterAction.Apply(playerHero);

        Campaign.Current.PlayerDefaultFaction = playerHero.Clan;

        // Used to MainHero and CharacterObject
        Game.Current.PlayerTroop = playerHero.CharacterObject;

        // Recapture if previously captured
        if (playerHero.PartyBelongedToAsPrisoner != null)
        {
            playerHero.PartyBelongedTo = null;
            messageBroker.Publish(this, new PlayerCaptivityChanged(playerHero.PartyBelongedToAsPrisoner));
        }
    }

    private void SetupNewHero(Hero hero)
    {
        SetupNewParty(hero);
        SetupHeroWithObjectManagers(hero);
    }

    private void SetupHeroWithObjectManagers(Hero hero)
    {
        var party = hero.PartyBelongedTo;

        RegisterObject(hero);
        RegisterObject(party);
        RegisterObject(hero.Clan);

        RegisterObject(party.StringId, party.ItemRoster);
        RegisterObject(party.StringId, party.Party);
        RegisterObject($"{nameof(MobileParty.MemberRoster)}_{party.StringId}", party.MemberRoster);
        RegisterObject($"{nameof(MobileParty.PrisonRoster)}_{party.StringId}", party.PrisonRoster);

        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        if (campaignObjectManager == null)
        {
            Logger.Error("{type} was null when trying to register a {managedType}", typeof(CampaignObjectManager), typeof(Hero));
            return;
        }

        campaignObjectManager.AddHero(hero);
        campaignObjectManager.AddMobileParty(party);

        var partyBase = party.Party;

        campaignObjectManager.AddClan(hero.Clan);

        partyBase.GetPartyVisual().OnStartup();
        partyBase.SetVisualAsDirty();
    }

    private void RegisterObject<T>(T obj) where T : MBObjectBase
    {
        using(new AllowedThread())
        {
            obj.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<T>($"Player");
            objectManager.AddExisting($"{typeof(T).Name}_{obj.StringId}", obj);
        }
    }

    private void RegisterObject(string prefix, object obj)
    {
        objectManager.AddExisting($"{obj.GetType().Name}_{prefix}", obj);
    }

    private void SetupNewParty(Hero hero)
    {
        var party = hero.PartyBelongedTo;

        using (new AllowedThread())
        {
            party.Anchor = new AnchorPoint(party);
        }
        
        party.IsVisible = true;  
        party.Party.SetVisualAsDirty();

        party.CheckPositionsForMapChangeAndUpdateIfNeeded();
        MobilePartyVisualManager.Current.AddNewPartyVisualForParty(party);
        CampaignEventDispatcher.Instance.OnPartyVisibilityChanged(party.Party);
    }
}
