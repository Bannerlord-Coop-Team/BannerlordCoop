using Common.Logging;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Players;

public interface IPlayerPartyRestorer
{
    bool TryRestore(Player player, out Player restoredPlayer);

    /// <summary>
    /// Resolves the party a saved row names if <see cref="KeepsParty"/> gives it to that row's hero.
    /// </summary>
    bool TryGetOwnedParty(Player player, IReadOnlyCollection<Hero> playerHeroes, out MobileParty party);

    /// <summary>
    /// True if the campaign still gives <paramref name="party"/> to <paramref name="hero"/>: the hero owns it, or the hero
    /// is dead and waiting for heir selection, and none of <paramref name="playerHeroes"/> owns the party now.
    /// </summary>
    bool KeepsParty(Hero hero, MobileParty party, IReadOnlyCollection<Hero> playerHeroes);

    void RestoreClanMembership(Hero hero);
    void Restore(Hero hero, MobileParty party);
}

internal class PlayerPartyRestorer : IPlayerPartyRestorer
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyRestorer>();

    private readonly IObjectManager objectManager;
    private readonly IAutoRegistryFactory autoRegistryFactory;
    private readonly Func<IEnumerable<MobileParty>> getParties;
    private readonly Func<Hero, MobileParty> createRecoveryParty;
    private readonly Action<MobileParty> removeRecoveryParty;

    public PlayerPartyRestorer(IObjectManager objectManager, IAutoRegistryFactory autoRegistryFactory)
        : this(objectManager, autoRegistryFactory, () => MobileParty.All, CreateRecoveryParty, RemoveParty)
    {
    }

    internal PlayerPartyRestorer(
        IObjectManager objectManager,
        IAutoRegistryFactory autoRegistryFactory,
        Func<IEnumerable<MobileParty>> getParties,
        Func<Hero, MobileParty> createRecoveryParty,
        Action<MobileParty> removeRecoveryParty)
    {
        this.objectManager = objectManager;
        this.autoRegistryFactory = autoRegistryFactory;
        this.getParties = getParties;
        this.createRecoveryParty = createRecoveryParty;
        this.removeRecoveryParty = removeRecoveryParty;
    }

    public bool TryRestore(Player player, out Player restoredPlayer)
    {
        restoredPlayer = player;
        if (player == null) return false;
        if (!objectManager.TryGetObjectWithLogging(player.HeroId, out Hero hero)) return false;
        // A saved row can name the hero by its short id, and hero ids are compared as strings everywhere else
        // (heir selection, sender checks), so the restored registration uses the id the hero is registered under
        if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return false;

        // A dead registered hero is waiting for heir selection. Keep its saved party association
        // intact instead of restoring the dead hero into the roster as its leader.
        if (hero.IsDead)
        {
            if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out MobileParty deadHeroParty)) return false;
            // Heir selection compares the heir's party id with this one, so it has to be the registered id too
            if (!objectManager.TryGetIdWithLogging(deadHeroParty, out var deadHeroPartyId)) return false;

            restoredPlayer = WithRegisteredIds(player, heroId, deadHeroPartyId, player.ClanId, player.CharacterObjectId);
            return true;
        }

        if (hero.Clan == null || hero.CharacterObject == null)
        {
            Logger.Error(
                "Cannot restore player {ControllerId}: hero {HeroId} has no clan or character object",
                player.ControllerId,
                player.HeroId);
            return false;
        }
        if (!objectManager.TryGetIdWithLogging(hero.Clan, out var clanId)) return false;
        if (!objectManager.TryGetIdWithLogging(hero.CharacterObject, out var characterObjectId)) return false;

        MobileParty party = null;
        if (objectManager.TryGetObject(player.MobilePartyId, out MobileParty savedParty) &&
            IsReadyPlayerParty(savedParty, hero))
        {
            party = savedParty;
        }
        else
        {
            party = getParties()
                .FirstOrDefault(candidate =>
                    IsReadyPlayerParty(candidate, hero) &&
                    objectManager.TryGetId(candidate, out _));

            if (party != null)
            {
                Logger.Warning(
                    "Reassociating controller {ControllerId} hero {HeroId} from stale party {SavedPartyId} to {PartyId}",
                    player.ControllerId,
                    player.HeroId,
                    player.MobilePartyId,
                    party.StringId);
            }
        }

        string partyId;
        var createdRecoveryParty = party == null;
        if (createdRecoveryParty)
        {
            party = createRecoveryParty(hero);
            if (party == null)
            {
                Logger.Error(
                    "Cannot restore player {ControllerId} hero {HeroId}: no valid party or recovery position exists",
                    player.ControllerId,
                    player.HeroId);
                return false;
            }

            Logger.Warning(
                "Created recovery party {PartyId} for controller {ControllerId} hero {HeroId}",
                party.StringId,
                player.ControllerId,
                player.HeroId);

            if (!TryRegisterRecoveryParty(player, party, out partyId))
                return false;
        }
        else if (!objectManager.TryGetIdWithLogging(party, out partyId))
        {
            return false;
        }

        // Vanilla clan and party calls can throw on a broken graph; a party this attempt created must not outlive it
        try
        {
            Restore(hero, party);
        }
        catch (Exception exception) when (createdRecoveryParty)
        {
            Logger.Error(
                exception,
                "Cannot restore player {ControllerId} hero {HeroId}: restoring into recovery party {PartyId} threw",
                player.ControllerId,
                player.HeroId,
                party.StringId);
            RemoveFailedRecoveryParty(player, party);
            return false;
        }

        restoredPlayer = WithRegisteredIds(player, heroId, partyId, clanId, characterObjectId);
        return true;
    }

    /// <inheritdoc/>
    public bool TryGetOwnedParty(Player player, IReadOnlyCollection<Hero> playerHeroes, out MobileParty party)
    {
        if (!objectManager.TryGetObject(player.MobilePartyId, out party) ||
            !objectManager.TryGetObject(player.HeroId, out Hero hero))
            return false;

        return KeepsParty(hero, party, playerHeroes);
    }

    /// <inheritdoc/>
    public bool KeepsParty(Hero hero, MobileParty party, IReadOnlyCollection<Hero> playerHeroes)
    {
        // Vanilla hands a dead hero's party to another hero in it, so the dead hero keeps it until another player owns it
        var owner = party.Owner;
        var heirSelectionPending = hero.IsDead && !playerHeroes.Any(playerHero => ReferenceEquals(playerHero, owner));
        return ReferenceEquals(owner, hero) || heirSelectionPending;
    }

    // The saved row itself when its ids are already the registered ones, otherwise a copy with them that keeps the
    // row's original clan and the fields that aren't campaign objects
    private static Player WithRegisteredIds(Player player, string heroId, string partyId, string clanId, string characterObjectId)
    {
        if (heroId == player.HeroId &&
            partyId == player.MobilePartyId &&
            clanId == player.ClanId &&
            characterObjectId == player.CharacterObjectId)
            return player;

        return new Player(player.ControllerId, heroId, partyId, clanId, characterObjectId, player.OriginalClanId ?? player.ClanId)
        {
            PlatformName = player.PlatformName,
            CrimeRatings = player.CrimeRatings,
            MapTrackedControllerIds = player.MapTrackedControllerIds
        };
    }

    private bool TryRegisterRecoveryParty(Player player, MobileParty party, out string partyId)
    {
        partyId = null;

        try
        {
            var registeredPartyId = string.Empty;
            var registered = objectManager.RunRegistrationTransaction(() =>
            {
                autoRegistryFactory.RegisterAll();
                return objectManager.TryGetIdWithLogging(party, out registeredPartyId);
            });

            partyId = registeredPartyId;
            if (registered)
                return true;
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Cannot restore player {ControllerId} hero {HeroId}: failed to register recovery party {PartyId}",
                player.ControllerId,
                player.HeroId,
                party.StringId);
            RemoveFailedRecoveryParty(player, party);
            return false;
        }

        Logger.Error(
            "Cannot restore player {ControllerId} hero {HeroId}: recovery party {PartyId} was not registered",
            player.ControllerId,
            player.HeroId,
            party.StringId);
        RemoveFailedRecoveryParty(player, party);
        return false;
    }

    private void RemoveFailedRecoveryParty(Player player, MobileParty party)
    {
        try
        {
            removeRecoveryParty(party);
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Failed to remove recovery party {PartyId} after restoration failed for controller {ControllerId} hero {HeroId}",
                party.StringId,
                player.ControllerId,
                player.HeroId);
        }
    }

    public void Restore(Hero hero, MobileParty party)
    {
        // Transferred root references can be missing from clan, roster, and party-component state.
        RestoreClanMembership(hero);

        if (hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null)
        {
            hero.PartyBelongedTo = null;

            if (party.LeaderHero != null)
                party.ChangePartyLeader(null);

            var heroCount = party.MemberRoster.GetTroopCount(hero.CharacterObject);
            if (heroCount > 0)
                party.MemberRoster.AddToCounts(hero.CharacterObject, -heroCount);

            var prisonerPosition = hero.GetCampaignPosition();
            if (prisonerPosition.IsValid())
                party.Position = prisonerPosition;

            party.IsActive = false;
            return;
        }

        hero.PartyBelongedTo = party;

        if (party.MemberRoster.GetTroopCount(hero.CharacterObject) == 0)
            party.MemberRoster.AddToCounts(hero.CharacterObject, 1, insertAtFront: true);

        if (party.LeaderHero != hero)
            party.ChangePartyLeader(hero);
    }

    public void RestoreClanMembership(Hero hero)
    {
        if (!hero.Clan.Heroes.Contains(hero))
            hero.Clan.OnLordAdded(hero);
    }

    private static bool IsReadyPlayerParty(MobileParty party, Hero hero)
    {
        if (party?.Party == null ||
            party.Party.MobileParty != party ||
            party.PartyComponent == null ||
            party.MemberRoster == null ||
            party.PrisonRoster == null ||
            party.ItemRoster == null ||
            party.LordPartyComponent?.Owner != hero)
        {
            return false;
        }

        if (party.PartyComponent.MobileParty == null)
            party.PartyComponent.MobileParty = party;

        return party.PartyComponent.MobileParty == party;
    }

    private static MobileParty CreateRecoveryParty(Hero hero)
    {
        var position = hero.GetCampaignPosition();
        if (!position.IsValid() && hero.LastKnownClosestSettlement != null)
            position = hero.LastKnownClosestSettlement.GatePosition;
        if (!position.IsValid()) return null;

        var recoveryArgs = new LordPartyComponent.InitializationArgs(position, 0, hero.LastKnownClosestSettlement);

        var component = new LordPartyComponent(hero, null, recoveryArgs);
        var party = MobileParty.CreateParty($"{hero.StringId}_recovered_{Guid.NewGuid():N}", component);
        party.InitializeMobilePartyAtPosition(position);
        return party;
    }

    private static void RemoveParty(MobileParty party)
    {
        party.RemoveParty();
    }
}
