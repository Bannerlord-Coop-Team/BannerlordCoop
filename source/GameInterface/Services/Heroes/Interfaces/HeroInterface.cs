using Common;
using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
    private readonly IPartyVisibilitySweep partyVisibilitySweep;

    public HeroInterface(
        IMessageBroker messageBroker,
        IBinaryPackageFactory binaryPackageFactory,
        IObjectManager objectManager,
        IPartyVisibilitySweep partyVisibilitySweep)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.binaryPackageFactory = binaryPackageFactory;
        this.partyVisibilitySweep = partyVisibilitySweep;
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

        GameThread.Run(() => {
            try
            {
                using (new AllowedThread())
                {
                    hero = BinaryFormatterSerializer
                        .Deserialize<HeroBinaryPackage>(bytes)
                        .Unpack<Hero>(binaryPackageFactory);

                    SetupNewHero(hero, assignNetworkIds);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to unpack hero");
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

        // Vanilla's character change ejects a main hero from its settlement, which would pop the
        // reloaded party outside on this client only (the server's save keeps it inside); keep it
        // inside and rebuild the local in-settlement state afterwards.
        LeaveSettlementActionPatches.SuppressForPlayerSwitch = true;
        try
        {
            ChangePlayerCharacterAction.Apply(playerHero);
        }
        finally
        {
            LeaveSettlementActionPatches.SuppressForPlayerSwitch = false;
        }

        if (playerParty.CurrentSettlement != null)
        {
            // Queued so it runs after the campaign state is entered; the headless server's save
            // carries no player encounter or menu state for this hero.
            GameThread.RunSafe(() =>
            {
                if (ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
                {
                    siegeEventInterface.RestoreReloadedPlayerInSettlement();
                }
            });
        }

        // Recapture if previously captured
        if (playerHero.PartyBelongedToAsPrisoner != null)
        {
            playerHero.PartyBelongedTo = null;
            messageBroker.Publish(this, new PlayerCaptivityChanged(playerHero.PartyBelongedToAsPrisoner));
        }

        // The transferred host save carries the server's always-visible party state
        // (PartyVisibilityServerPatches) and the native load path never rebuilds fog of war
        // (Campaign.GameInitTick only runs for new campaigns), so distant parties and their battle
        // icons would stay revealed forever. Queued so it runs once the campaign state is entered
        // and the hero switched to above is the local main party.
        GameThread.RunSafe(partyVisibilitySweep.RebuildAroundMainParty);
    }

    private void SetupNewHero(Hero hero, Action<Hero> assignNetworkIds)
    {
        var party = hero.PartyBelongedTo;

        party.Anchor = new AnchorPoint(party);

        party.Party.OnFinishLoadState();

        party.CheckPositionsForMapChangeAndUpdateIfNeeded();

        // On the server this write is coerced to always-visible by PartyVisibilityOnServerPatch;
        // on clients it computes the real fog-of-war state so a remote player's new party is not
        // permanently revealed wherever it spawned (the native per-tick sweep only re-evaluates
        // parties near the local main party).
        if (MobileParty.MainParty != null)
        {
            party.Party.UpdateVisibilityAndInspected(MobileParty.MainParty.Position);
        }
        else
        {
            party.IsVisible = true;
        }

        // Headless hosts run without the SandBox.View layer, so the visual manager is null there
        // and party visuals are optional (same contract as PartyBaseExtensions.GetPartyVisual).
        // An unguarded call here aborts the whole hero setup before its network ids are assigned.
        if (MobilePartyVisualManager.Current != null)
            MobilePartyVisualManager.Current.AddNewPartyVisualForParty(party);

        CampaignEventDispatcher.Instance.OnPartyVisibilityChanged(party.Party);

        // Add to game managed lists
        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        if (campaignObjectManager == null)
        {
            Logger.Error("{type} was null when trying to register a {managedType}", typeof(CampaignObjectManager), typeof(Hero));
            return;
        }

        // Restore the roster before assignNetworkIds registers it. Otherwise the AllowedThread AddToCounts
        // patch sends a roster update before clients receive the hero creation message.
        RestorePlayerMemberships(hero, party);

        // Assign the network StringIds BEFORE adding to the CampaignObjectManager. FindNextUniqueStringId derives
        // the next "PlayerN" from CampaignObjectType.MaxCreatedPostfixIndex, which is cached in OnItemAdded when an
        // object is *added* (using the StringId at that instant). If we add first (with the deserialized
        // "main_hero" id) and rename afterwards, that cache never learns about the assigned "PlayerN", so the next
        // hero computes — and collides with — the same id.
        assignNetworkIds(hero);

        // The party's visual was created above inside this AllowedThread, which suppressed the patch that
        // registers it. Register it under the party's derived id, now that the StringId is final, so the host
        // and already-connected clients (which run this same unpack for a late joiner) resolve it by one id.
        var partyVisual = party.Party.GetPartyVisual();
        if (partyVisual != null)
            objectManager.AddExisting($"{nameof(MobilePartyVisual)}_{party.StringId}", partyVisual);

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

    internal static void RestorePlayerMemberships(Hero hero, MobileParty party)
    {
        // PackageMainHero unregisters the player hero before packaging, so ClanBinaryPackage cannot store its
        // network ID in the clan's hero and alive-lord caches.
        if (!hero.Clan.Heroes.Contains(hero))
            hero.Clan.OnLordAdded(hero);

        // TroopRosterBinaryPackage excludes roster elements, so the imported party has an empty member roster.
        if (party.MemberRoster.GetTroopCount(hero.CharacterObject) == 0)
            party.MemberRoster.AddToCounts(hero.CharacterObject, 1, insertAtFront: true);
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
        RegisterPrimary(hero.CharacterObject, hero.StringId);

        // HeroDeveloper is not a child of MBObjectBase, can't use RegisterPrimary
        objectManager.AddExisting($"{nameof(HeroDeveloper)}_{hero.StringId}", hero.HeroDeveloper); 

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

        // HeroDeveloper is not a child of MBObjectBase, can't use RegisterPrimary
        objectManager.AddExisting($"{nameof(HeroDeveloper)}_{StripTypePrefix(player.HeroId, hero)}", hero.HeroDeveloper);

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
