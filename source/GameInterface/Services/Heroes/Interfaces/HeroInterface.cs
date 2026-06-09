using Common;
using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
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
    void SwitchToPlayer(Player player);
    Hero UnpackHero(byte[] bytes);
    void CreateAndAssignHeroNetworkIds(Hero hero);
    void SetupNewHero(Hero hero);
}

internal class HeroInterface : IHeroInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly IBinaryPackageFactory binaryPackageFactory;

    public HeroInterface(
        IMessageBroker messageBroker,
        IBinaryPackageFactory binaryPackageFactory,
        IObjectManager objectManager)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.binaryPackageFactory = binaryPackageFactory;
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

    public Hero UnpackHero(byte[] bytes)
    {
        Hero hero = null;

        GameLoopRunner.RunOnMainThread(() => {
            using (new AllowedThread())
            {
                hero = BinaryFormatterSerializer
                    .Deserialize<HeroBinaryPackage>(bytes)
                    .Unpack<Hero>(binaryPackageFactory);
            }
        },
        blocking: true);

        return hero;
    }

    public void SwitchToPlayer(Player player)
    {
        if (!objectManager.TryGetObjectWithLogging(player.HeroId, out Hero playerHero))
            return;
        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out MobileParty playerParty))
            return;

        Campaign.Current.MainParty = playerParty;
        Campaign.Current.PlayerDefaultFaction = playerHero.Clan;

        // Used to MainHero and CharacterObject
        Game.Current.PlayerTroop = playerHero.CharacterObject;
        // This is needed because if the player is captured the PartyBelongedTo is null
        // Causing ChangePlayerCharacterAction to fail
        playerHero.PartyBelongedTo = playerParty;

        Logger.Information("Switching to new hero: {heroName}", playerHero.Name.ToString());

        ChangePlayerCharacterAction.Apply(playerHero);

        // Recapture if previously captured
        if (playerHero.PartyBelongedToAsPrisoner != null)
        {
            playerHero.PartyBelongedTo = null;
            messageBroker.Publish(this, new PlayerCaptivityChanged(playerHero.PartyBelongedToAsPrisoner));
        }
    }

    public void SetupNewHero(Hero hero)
    {
        var party = hero.PartyBelongedTo;

        using (new AllowedThread())
        {
            party.Anchor = new AnchorPoint(party);
        }

        party.Party.OnFinishLoadState();
        party.IsVisible = true;

        party.CheckPositionsForMapChangeAndUpdateIfNeeded();
        MobilePartyVisualManager.Current.AddNewPartyVisualForParty(party);
        CampaignEventDispatcher.Instance.OnPartyVisibilityChanged(party.Party);

        // Add to game managed lists
        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        if (campaignObjectManager == null)
        {
            Logger.Error("{type} was null when trying to register a {managedType}", typeof(CampaignObjectManager), typeof(Hero));
            return;
        }

        campaignObjectManager.AddHero(hero);
        campaignObjectManager.AddMobileParty(party);
        campaignObjectManager.AddClan(hero.Clan);
    }

    public void CreateAndAssignHeroNetworkIds(Hero hero)
    {
        var party = hero.PartyBelongedTo;

        var heroId = RegisterObject(hero);
        var partyId = RegisterObject(party);
        var clanId = RegisterObject(hero.Clan);
        var characterObjectId = RegisterObject(hero.CharacterObject);

        RegisterObject(party.StringId, party.ItemRoster);
        RegisterObject(party.StringId, party.Party);
        RegisterObject($"{nameof(MobileParty.MemberRoster)}_{party.StringId}", party.MemberRoster);
        RegisterObject($"{nameof(MobileParty.PrisonRoster)}_{party.StringId}", party.PrisonRoster);
    }

    private string RegisterObject<T>(T obj) where T : MBObjectBase
    {
        using (new AllowedThread())
        {
            obj.StringId = $"Player_{objectManager.GetUniqueTypeId(obj)}";
        }

        var id = $"{typeof(T).Name}_{obj.StringId}";

        objectManager.AddExisting(id, obj);

        return id;
    }

    private void RegisterObject(string prefix, object obj)
    {
        objectManager.AddExisting($"{obj.GetType().Name}_{prefix}", obj);
    }
}
