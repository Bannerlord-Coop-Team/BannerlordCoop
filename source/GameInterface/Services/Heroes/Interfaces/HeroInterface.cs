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
using System;
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
    Hero ServerUnpackHero(byte[] bytes);
    Hero ClientUnpackHero(byte[] bytes, Player player);
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

    public Hero ServerUnpackHero(byte[] bytes)
    {
        // Host: unpack and fully set up the hero on the main thread, assigning fresh "Player" network ids.
        return UnpackHero(bytes, AssignServerHeroNetworkIds);
    }

    public Hero ClientUnpackHero(byte[] bytes, Player player)
    {
        // Client: unpack and set up on the main thread, reusing the ids the host already assigned (carried by
        // the Player). Unpacking and setup MUST happen in one main-thread pass — splitting them across threads
        // corrupts the campaign's object/StringId bookkeeping and the next save.
        return UnpackHero(bytes, hero => AssignClientHeroNetworkIds(hero, player));
    }

    private Hero UnpackHero(byte[] bytes, Action<Hero> assignNetworkIds)
    {
        Hero hero = null;

        GameLoopRunner.RunOnMainThread(() => {
            using (new AllowedThread())
            {
                hero = BinaryFormatterSerializer
                    .Deserialize<HeroBinaryPackage>(bytes)
                    .Unpack<Hero>(binaryPackageFactory);

                SetupNewHero(hero, assignNetworkIds);
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

    private void SetupNewHero(Hero hero, Action<Hero> assignNetworkIds)
    {
        var party = hero.PartyBelongedTo;

        party.Anchor = new AnchorPoint(party);

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

        // Assign the network StringIds BEFORE adding to the CampaignObjectManager. FindNextUniqueStringId derives
        // the next "PlayerN" from CampaignObjectType.MaxCreatedPostfixIndex, which is cached in OnItemAdded when an
        // object is *added* (using the StringId at that instant). If we add first (with the deserialized
        // "main_hero" id) and rename afterwards, that cache never learns about the assigned "PlayerN", so the next
        // hero computes — and collides with — the same id.
        assignNetworkIds(hero);

        // The unpacked CharacterObject still carries the sender's MBGUID and IsRegistered flag. The Add* calls
        // below re-mint hero/party/clan ids, but a CharacterObject is owned by MBObjectManager, which only mints
        // ids on registration — so register it (after its StringId is final). Left unregistered, the transfer
        // save's load either skips it silently (hero falls back to the default character) or, with
        // a unique StringId, crashes adding the foreign MBGUID to the registry's GUID table.
        var characterObject = hero.CharacterObject;
        MBObjectManager.Instance?.RegisterObject(characterObject);
        if (characterObject.StringId != hero.StringId)
            Logger.Error("CharacterObject was renamed to {newId} during registration; expected {expectedId}",
                characterObject.StringId, hero.StringId);

        campaignObjectManager.AddHero(hero);
        campaignObjectManager.AddMobileParty(party);
        campaignObjectManager.AddClan(hero.Clan);
    }

    /// <summary>
    /// Host: assign fresh, campaign-unique "Player" StringIds to the hero graph and register them.
    /// </summary>
    private void AssignServerHeroNetworkIds(Hero hero)
    {
        var party = hero.PartyBelongedTo;

        RegisterPrimary(hero, NewServerStringId(hero));
        RegisterPrimary(party, NewServerStringId(party));
        RegisterPrimary(hero.Clan, NewServerStringId(hero.Clan));
        // CharacterObject is 1:1 with its hero and isn't tracked by the CampaignObjectManager, so we can't mint a
        // "PlayerN" id for it via FindNextUniqueStringId (that only knows campaign object types). Reuse the hero's
        // freshly-assigned unique StringId; the type-prefixed coop key ("CharacterObject_PlayerN") keeps it distinct.
        RegisterPrimary(hero.CharacterObject, hero.StringId);

        RegisterPartyChildren(party);
    }

    /// <summary>
    /// Client: reuse the ids the host already chose (carried by <paramref name="player"/>). The same StringIds
    /// are stamped onto the received objects so every derived id and the campaign bookkeeping match the host.
    /// </summary>
    private void AssignClientHeroNetworkIds(Hero hero, Player player)
    {
        var party = hero.PartyBelongedTo;

        RegisterPrimary(hero, StripTypePrefix(player.HeroId, hero));
        RegisterPrimary(party, StripTypePrefix(player.MobilePartyId, party));
        RegisterPrimary(hero.Clan, StripTypePrefix(player.ClanId, hero.Clan));
        RegisterPrimary(hero.CharacterObject, StripTypePrefix(player.CharacterObjectId, hero.CharacterObject));

        RegisterPartyChildren(party);
    }

    private string NewServerStringId<T>(T obj) where T : MBObjectBase
        => Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<T>("Player");

    private void RegisterPrimary<T>(T obj, string stringId) where T : MBObjectBase
    {
        // Caller runs inside the unpack's AllowedThread/main-thread pass, so the StringId set is permitted.
        obj.StringId = stringId;
        objectManager.AddExisting($"{typeof(T).Name}_{obj.StringId}", obj);
    }

    private void RegisterPartyChildren(MobileParty party)
    {
        // PartyBase + rosters have no StringId of their own; key them off the party's so host and client match.
        RegisterChild(party.ItemRoster, party.StringId);
        RegisterChild(party.Party, party.StringId);
        RegisterChild(party.MemberRoster, $"{nameof(MobileParty.MemberRoster)}_{party.StringId}");
        RegisterChild(party.PrisonRoster, $"{nameof(MobileParty.PrisonRoster)}_{party.StringId}");
    }

    private void RegisterChild(object obj, string suffix)
    {
        objectManager.AddExisting($"{obj.GetType().Name}_{suffix}", obj);
    }

    /// <summary>
    /// Recovers the StringId from a registered id (e.g. "MobileParty_Player1" -> "Player1") by stripping the
    /// leading "{TypeName}_" prefix the registration scheme adds.
    /// </summary>
    private static string StripTypePrefix<T>(string registeredId, T obj) where T : MBObjectBase
    {
        var prefix = $"{typeof(T).Name}_";
        return registeredId != null && registeredId.StartsWith(prefix)
            ? registeredId.Substring(prefix.Length)
            : registeredId;
    }
}
