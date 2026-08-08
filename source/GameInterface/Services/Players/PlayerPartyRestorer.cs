using Common.Logging;
using GameInterface.Registry;
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
    void Restore(Hero hero, MobileParty party);
}

internal class PlayerPartyRestorer : IPlayerPartyRestorer
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyRestorer>();

    private readonly IObjectManager objectManager;
    private readonly IRegistryManager registryManager;
    private readonly Func<IEnumerable<MobileParty>> getParties;
    private readonly Func<Hero, MobileParty> createParty;
    private readonly Action<MobileParty> removeParty;

    public PlayerPartyRestorer(IObjectManager objectManager, IRegistryManager registryManager)
        : this(objectManager, registryManager, () => MobileParty.All, CreateReplacementParty, RemoveParty)
    {
    }

    internal PlayerPartyRestorer(
        IObjectManager objectManager,
        IRegistryManager registryManager,
        Func<IEnumerable<MobileParty>> getParties,
        Func<Hero, MobileParty> createParty,
        Action<MobileParty> removeParty)
    {
        this.objectManager = objectManager;
        this.registryManager = registryManager;
        this.getParties = getParties;
        this.createParty = createParty;
        this.removeParty = removeParty;
    }

    public bool TryRestore(Player player, out Player restoredPlayer)
    {
        restoredPlayer = player;
        if (player == null) return false;
        if (!objectManager.TryGetObjectWithLogging(player.HeroId, out Hero hero)) return false;
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
        if (party == null)
        {
            party = createParty(hero);
            if (party == null)
            {
                Logger.Error(
                    "Cannot restore player {ControllerId} hero {HeroId}: no valid party or recovery position exists",
                    player.ControllerId,
                    player.HeroId);
                return false;
            }

            Logger.Warning(
                "Created replacement party {PartyId} for controller {ControllerId} hero {HeroId}",
                party.StringId,
                player.ControllerId,
                player.HeroId);

            if (!TryRegisterReplacementParty(player, party, out partyId))
                return false;
        }
        else if (!objectManager.TryGetIdWithLogging(party, out partyId))
        {
            return false;
        }

        Restore(hero, party);

        if (partyId != player.MobilePartyId ||
            clanId != player.ClanId ||
            characterObjectId != player.CharacterObjectId)
        {
            restoredPlayer = new Player(
                player.ControllerId,
                player.HeroId,
                partyId,
                clanId,
                characterObjectId);
        }

        return true;
    }

    private bool TryRegisterReplacementParty(Player player, MobileParty party, out string partyId)
    {
        partyId = null;

        try
        {
            var registeredPartyId = string.Empty;
            var registered = objectManager.RunRegistrationTransaction(() =>
            {
                registryManager.RegisterUntrackedGameObjects();
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
                "Cannot restore player {ControllerId} hero {HeroId}: failed to register replacement party {PartyId}",
                player.ControllerId,
                player.HeroId,
                party.StringId);
            RemoveFailedReplacement(player, party);
            return false;
        }

        Logger.Error(
            "Cannot restore player {ControllerId} hero {HeroId}: replacement party {PartyId} was not registered",
            player.ControllerId,
            player.HeroId,
            party.StringId);
        RemoveFailedReplacement(player, party);
        return false;
    }

    private void RemoveFailedReplacement(Player player, MobileParty party)
    {
        try
        {
            removeParty(party);
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Failed to remove replacement party {PartyId} after restoration failed for controller {ControllerId} hero {HeroId}",
                party.StringId,
                player.ControllerId,
                player.HeroId);
        }
    }

    public void Restore(Hero hero, MobileParty party)
    {
        // Transferred root references can be missing from clan, roster, and party-component state.
        if (!hero.Clan.Heroes.Contains(hero))
            hero.Clan.OnLordAdded(hero);

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

    private static MobileParty CreateReplacementParty(Hero hero)
    {
        var position = hero.GetCampaignPosition();
        if (!position.IsValid() && hero.LastKnownClosestSettlement != null)
            position = hero.LastKnownClosestSettlement.GatePosition;
        if (!position.IsValid()) return null;

        var component = new LordPartyComponent(hero, null, null);
        var party = MobileParty.CreateParty($"{hero.StringId}_recovered_{Guid.NewGuid():N}", component);
        party.InitializeMobilePartyAtPosition(position);
        return party;
    }

    private static void RemoveParty(MobileParty party)
    {
        party.RemoveParty();
    }
}
