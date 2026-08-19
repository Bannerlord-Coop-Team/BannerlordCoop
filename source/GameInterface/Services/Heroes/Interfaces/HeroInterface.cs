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
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using SandBox.View.Map.Managers;
using Serilog;
using System;
using System.Runtime.ExceptionServices;
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
    void SetupServerHero(Hero hero);
    Hero ClientUnpackHero(byte[] bytes, Player player);
}

internal class HeroInterface : IHeroInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly IBinaryPackageFactory binaryPackageFactory;
    private readonly IPartyVisibilitySweep partyVisibilitySweep;
    private readonly IPlayerPartyRestorer playerPartyRestorer;

    public HeroInterface(
        IMessageBroker messageBroker,
        IBinaryPackageFactory binaryPackageFactory,
        IObjectManager objectManager,
        IPartyVisibilitySweep partyVisibilitySweep,
        IPlayerPartyRestorer playerPartyRestorer)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.binaryPackageFactory = binaryPackageFactory;
        this.partyVisibilitySweep = partyVisibilitySweep;
        this.playerPartyRestorer = playerPartyRestorer;
    }

    public byte[] PackageMainHero()
    {
        objectManager.Remove(Hero.MainHero);
        objectManager.Remove(Hero.MainHero.PartyBelongedTo);
        objectManager.Remove(Hero.MainHero.Clan);
        objectManager.Remove(Hero.MainHero.CharacterObject);

        HeroBinaryPackage package = binaryPackageFactory.GetBinaryPackage<HeroBinaryPackage>(Hero.MainHero);

        return BinaryPackageSerializer.SerializeCompressed(package);
    }

    public Hero ServerUnpackHero(byte[] bytes)
    {
        // Prepare the identity before the creation broadcast. Authoritative setup runs afterwards with patches live.
        return UnpackHero(bytes, AssignServerHeroNetworkIds, setupHero: false);
    }

    public void SetupServerHero(Hero hero)
    {
        ExceptionDispatchInfo exception = null;

        GameThread.Run(() =>
        {
            try
            {
                SetupNewHero(hero, restoreParty: true);
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(e);
            }
        }, blocking: true);

        exception?.Throw();
    }

    public Hero ClientUnpackHero(byte[] bytes, Player player)
    {
        // The client applies the received creation as one suppressed main-thread operation.
        return UnpackHero(bytes, hero => AssignClientHeroNetworkIds(hero, player), setupHero: true);
    }

    private Hero UnpackHero(byte[] bytes, Action<Hero> assignNetworkIds, bool setupHero)
    {
        Hero hero = null;

        GameThread.Run(() => {
            try
            {
                using (new AllowedThread())
                {
                    hero = BinaryPackageSerializer
                        .DeserializeCompressed<HeroBinaryPackage>(bytes)
                        .Unpack<Hero>(binaryPackageFactory);

                    assignNetworkIds(hero);

                    if (setupHero)
                        SetupNewHero(hero, restoreParty: false);
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

        LogPlayerSwitchState("before", player, playerHero, playerParty);

        Campaign.Current.MainParty = playerParty;
        Campaign.Current.PlayerDefaultFaction = playerHero.Clan;

        // Used to MainHero and CharacterObject
        Game.Current.PlayerTroop = playerHero.CharacterObject;
        // This is needed because if the player is captured the PartyBelongedTo is null
        // Causing ChangePlayerCharacterAction to fail
        playerHero.PartyBelongedTo = playerParty;

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

        LogPlayerSwitchState("after", player, playerHero, playerParty);

        if (playerParty.CurrentSettlement != null || playerParty.BesiegerCamp != null)
        {
            // Queued so it runs after the campaign state is entered; the headless server's save
            // carries no player encounter, menu, or player-siege state for this hero. Covers both
            // a party reloaded inside a settlement and one besieging a settlement, which would
            // otherwise sit at the siege camp with no menu (soft lock).
            GameThread.RunSafe(() =>
            {
                if (ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
                {
                    siegeEventInterface.RestoreReloadedPlayer();
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

    private void LogPlayerSwitchState(
        string phase,
        Player player,
        Hero playerHero,
        MobileParty playerParty)
    {
        bool heroRegistered = objectManager.TryGetId(playerHero, out string registeredHeroId);
        bool partyRegistered = objectManager.TryGetId(playerParty, out string registeredPartyId);
        bool partyInCampaign = Campaign.Current?.MobileParties?.Contains(playerParty) == true;

        Logger.Information(
            "Player switch {Phase}: hero={HeroName} requestedHero={RequestedHeroId} " +
            "registeredHero={RegisteredHeroId} heroRegistered={HeroRegistered} " +
            "heroParty={HeroPartyId} requestedParty={RequestedPartyId} " +
            "registeredParty={RegisteredPartyId} partyRegistered={PartyRegistered} " +
            "mainHero={MainHeroId} mainParty={MainPartyId} inCampaign={PartyInCampaign} " +
            "active={PartyActive} roster={RosterCount} mapEvent={MapEventId} " +
            "settlement={SettlementId} moveMode={MoveMode} targetParty={TargetPartyId} " +
            "targetSettlement={TargetSettlementId} moveTargetParty={MoveTargetPartyId} " +
            "interactable={InteractableType}",
            phase,
            playerHero.Name?.ToString(),
            player.HeroId,
            heroRegistered ? registeredHeroId : "missing",
            heroRegistered,
            playerHero.PartyBelongedTo?.StringId,
            player.MobilePartyId,
            partyRegistered ? registeredPartyId : "missing",
            partyRegistered,
            Hero.MainHero?.StringId,
            MobileParty.MainParty?.StringId,
            partyInCampaign,
            playerParty.IsActive,
            playerParty.MemberRoster?.TotalManCount ?? -1,
            playerParty.MapEvent?.StringId,
            playerParty.CurrentSettlement?.StringId,
            playerParty.PartyMoveMode,
            playerParty.TargetParty?.StringId,
            playerParty.TargetSettlement?.StringId,
            playerParty.MoveTargetParty?.StringId,
            playerParty.Ai?.AiBehaviorInteractable?.GetType().Name);
    }

    private void SetupNewHero(Hero hero, bool restoreParty)
    {
        var party = hero.PartyBelongedTo;

        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        if (campaignObjectManager == null)
        {
            throw new InvalidOperationException(
                $"{typeof(CampaignObjectManager)} was null when trying to register a {typeof(Hero)}");
        }

        // These registrations assign the final MBGUIDs used by MBObjectBase.GetHashCode. Finish them before
        // any setup operation can add a faction stance keyed by the imported clan.
        var characterObject = hero.CharacterObject;
        MBObjectManager.Instance?.RegisterObject(characterObject);
        if (characterObject.StringId != hero.StringId)
            Logger.Error("CharacterObject was renamed to {newId} during registration; expected {expectedId}",
                characterObject.StringId, hero.StringId);

        campaignObjectManager.AddClan(hero.Clan);
        campaignObjectManager.AddHero(hero);
        campaignObjectManager.AddMobileParty(party);

        // Player birth dates come from a separate character-creation campaign, so rebase them to this campaign.
        hero.SetBirthDay(CampaignTime.YearsFromNow(-hero._defaultAge));

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

        // Clan membership is not replicated, but roster and leadership repairs are server-authoritative.
        if (restoreParty)
            playerPartyRestorer.Restore(hero, party);
        else
            playerPartyRestorer.RestoreClanMembership(hero);
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
