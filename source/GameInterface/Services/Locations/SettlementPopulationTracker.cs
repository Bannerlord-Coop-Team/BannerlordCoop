using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Locations;

/// <summary>
/// Server-side orchestrator that populates settlement location rosters when player parties visit.
/// The vanilla population logic is gated on the local player being inside the settlement, which is
/// never true on the server, so this tracker drives the equivalent hero placement from the synced
/// settlement enter/leave messages; the patched mutators broadcast every resulting change.
/// A disconnected player's party stays parked inside its settlement, so it intentionally keeps
/// counting as a visitor here - the rosters then keep matching the actual campaign state.
/// </summary>
internal class SettlementPopulationTracker : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementPopulationTracker>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    // Keyed by Settlement.StringId; holds the settlement so cleanup never rescans the campaign.
    private readonly Dictionary<string, Settlement> populatedSettlements = new Dictionary<string, Settlement>();
    private readonly Dictionary<string, string> playerPartySettlements = new Dictionary<string, string>();
    private readonly Dictionary<string, List<LocationCharacterEntry>> partyCompanionEntries =
        new Dictionary<string, List<LocationCharacterEntry>>();

    private readonly struct LocationCharacterEntry
    {
        public readonly Location Location;
        public readonly LocationCharacter Entry;

        public LocationCharacterEntry(Location location, LocationCharacter entry)
        {
            Location = location;
            Entry = entry;
        }
    }

    public SettlementPopulationTracker(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<PartyEnterSettlement>(Handle_PartyEnterSettlement);
        messageBroker.Subscribe<PartyLeaveSettlement>(Handle_PartyLeaveSettlement);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyEnterSettlement>(Handle_PartyEnterSettlement);
        messageBroker.Unsubscribe<PartyLeaveSettlement>(Handle_PartyLeaveSettlement);
    }

    private void Handle_PartyEnterSettlement(MessagePayload<PartyEnterSettlement> payload)
    {
        if (ModInformation.IsServer == false) return;

        var settlementId = payload.What.SettlementId;
        var partyId = payload.What.PartyId;

        if (objectManager.TryGetObjectWithLogging(settlementId, out Settlement settlement) == false) return;
        if (objectManager.TryGetObjectWithLogging(partyId, out MobileParty party) == false) return;
        if (settlement.LocationComplex == null) return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            if (party.IsPlayerParty())
            {
                playerPartySettlements[partyId] = settlementId;

                if (populatedSettlements.ContainsKey(settlement.StringId) == false)
                {
                    populatedSettlements.Add(settlement.StringId, settlement);
                    PopulateSettlement(settlement);
                }

                AddCompanionEntries(partyId, party, settlement);
                BroadcastRosterSnapshot(settlement, settlementId);
            }
            else if (HasPlayerVisitors(settlementId) && party.LeaderHero != null)
            {
                // An AI lord arriving while players are inside; vanilla's placement driver is
                // inert on the server, so place the leader explicitly.
                RefreshHeroPlacement(party.LeaderHero, settlement);
            }
        });
    }

    private void Handle_PartyLeaveSettlement(MessagePayload<PartyLeaveSettlement> payload)
    {
        if (ModInformation.IsServer == false) return;

        var partyId = payload.What.PartyId;

        GameLoopRunner.RunOnMainThread(() =>
        {
            if (playerPartySettlements.TryGetValue(partyId, out var settlementId) == false)
            {
                RemoveAiLeaderPlacement(partyId);
                return;
            }

            playerPartySettlements.Remove(partyId);
            RemoveCompanionEntries(partyId);

            if (objectManager.TryGetObjectWithLogging(settlementId, out Settlement settlement) == false) return;

            if (HasPlayerVisitors(settlementId) == false)
            {
                ClearSettlementRosters(settlement);
                populatedSettlements.Remove(settlement.StringId);
            }
        });
    }

    /// <summary>
    /// Allows the debug command to populate and broadcast a settlement without a visiting party.
    /// </summary>
    public void DebugPopulate(Settlement settlement)
    {
        if (ModInformation.IsServer == false) return;
        if (settlement?.LocationComplex == null) return;
        if (objectManager.TryGetIdWithLogging(settlement, out var settlementId) == false) return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            if (populatedSettlements.ContainsKey(settlement.StringId) == false)
            {
                populatedSettlements.Add(settlement.StringId, settlement);
                PopulateSettlement(settlement);
            }

            BroadcastRosterSnapshot(settlement, settlementId);
        });
    }

    // The visitor bookkeeping is deliberately message-driven rather than derived from
    // Settlement.Parties: the game-state apply of an enter/leave runs asynchronously on the main
    // thread, so the live party list may not reflect the message that is being handled yet.
    private bool HasPlayerVisitors(string settlementId)
    {
        return playerPartySettlements.ContainsValue(settlementId);
    }

    private void PopulateSettlement(Settlement settlement)
    {
        var behavior = Campaign.Current?.GetCampaignBehavior<HeroAgentSpawnCampaignBehavior>();
        if (behavior == null)
        {
            Logger.Warning("HeroAgentSpawnCampaignBehavior not found; cannot populate {Settlement}", settlement.StringId);
            return;
        }

        foreach (var hero in CollectHeroesToPlace(settlement))
        {
            try
            {
                behavior.RefreshLocationOfHeroForSettlement(hero, settlement);
            }
            catch (Exception e)
            {
                Logger.Warning(e, "Failed to place {Hero} in {Settlement}", hero.StringId, settlement.StringId);
            }
        }
    }

    private static IEnumerable<Hero> CollectHeroesToPlace(Settlement settlement)
    {
        var heroes = new HashSet<Hero>();

        foreach (var hero in settlement.HeroesWithoutParty ?? Enumerable.Empty<Hero>())
        {
            heroes.Add(hero);
        }

        foreach (var party in settlement.Parties ?? (IReadOnlyList<MobileParty>)new List<MobileParty>())
        {
            if (party.LeaderHero == null) continue;
            if (party.IsPlayerParty()) continue;

            heroes.Add(party.LeaderHero);
        }

        try
        {
            foreach (var character in settlement.SettlementComponent?.GetPrisonerHeroes()
                ?? Enumerable.Empty<CharacterObject>())
            {
                if (character.HeroObject != null)
                {
                    heroes.Add(character.HeroObject);
                }
            }
        }
        catch (Exception)
        {
            // Prisoner enumeration depends on the settlement component type; skip when unavailable.
        }

        // Player heroes are the player agents themselves and are never placed as roster NPCs.
        return heroes.Where(hero => hero != null && PlayerManager.TryGetControlledObjectInfo(hero, out _) == false);
    }

    private void RefreshHeroPlacement(Hero hero, Settlement settlement)
    {
        var behavior = Campaign.Current?.GetCampaignBehavior<HeroAgentSpawnCampaignBehavior>();
        if (behavior == null) return;

        try
        {
            behavior.RefreshLocationOfHeroForSettlement(hero, settlement);
        }
        catch (Exception e)
        {
            Logger.Warning(e, "Failed to place {Hero} in {Settlement}", hero.StringId, settlement.StringId);
        }
    }

    private void AddCompanionEntries(string partyId, MobileParty party, Settlement settlement)
    {
        var locationComplex = settlement.LocationComplex;
        var targetLocation = locationComplex.GetLocationWithId("tavern") ?? locationComplex.GetLocationWithId("center");
        if (targetLocation == null) return;

        if (party.MemberRoster == null) return;

        var entries = new List<LocationCharacterEntry>();

        foreach (var rosterElement in party.MemberRoster.GetTroopRoster())
        {
            var character = rosterElement.Character;
            if (character == null || character.IsHero == false) continue;

            var hero = character.HeroObject;
            if (hero == null || hero == party.LeaderHero || hero.IsAlive == false) continue;
            if (PlayerManager.TryGetControlledObjectInfo(hero, out _)) continue;

            var entry = LocationCharacterFactory.CreateCompanion(hero, party, useCivilianEquipment: settlement.IsVillage == false);

            // The patched mutator publishes the broadcast for this add.
            targetLocation.AddCharacter(entry);
            entries.Add(new LocationCharacterEntry(targetLocation, entry));
        }

        if (entries.Count > 0)
        {
            partyCompanionEntries[partyId] = entries;
        }
    }

    private void RemoveCompanionEntries(string partyId)
    {
        if (partyCompanionEntries.TryGetValue(partyId, out var entries) == false) return;

        partyCompanionEntries.Remove(partyId);

        foreach (var entry in entries)
        {
            // List.Remove on an absent entry is a no-op, so no containment check is needed.
            entry.Location.RemoveLocationCharacter(entry.Entry);
        }
    }

    private void RemoveAiLeaderPlacement(string partyId)
    {
        if (objectManager.TryGetObject(partyId, out MobileParty party) == false) return;

        var leaderHero = party?.LeaderHero;
        if (leaderHero == null || party.IsPlayerParty()) return;

        // The leave may already have been applied, so the party's settlement can be gone; check
        // every populated settlement instead (there are only as many as there are visited towns).
        foreach (var settlement in populatedSettlements.Values)
        {
            settlement.LocationComplex?.RemoveCharacterIfExists(leaderHero);
        }
    }

    private void ClearSettlementRosters(Settlement settlement)
    {
        foreach (var location in settlement.LocationComplex.GetListOfLocations())
        {
            if (location.GetCharacterList()?.Any() != true) continue;

            // The patched mutator publishes the broadcast for this clear.
            location.RemoveAllCharacters();
        }
    }

    private void BroadcastRosterSnapshot(Settlement settlement, string settlementId)
    {
        var entries = new List<LocationCharacterData>();

        foreach (var location in settlement.LocationComplex.GetListOfLocations())
        {
            if (objectManager.TryGetId(location, out var locationId) == false) continue;

            foreach (var locationCharacter in location.GetCharacterList() ?? Enumerable.Empty<LocationCharacter>())
            {
                // Players are represented inside interiors by the P2P mission layer, never by the
                // location roster. Excluding them here (server is authoritative on who is a player)
                // keeps clients from spawning a frozen roster duplicate next to the P2P agent.
                var hero = locationCharacter?.Character?.HeroObject;
                if (hero != null && hero.IsPlayerHero()) continue;

                if (LocationCharacterFactory.TryCreateData(objectManager, locationId, locationCharacter, out var data))
                {
                    entries.Add(data);
                }
            }
        }

        network.SendAll(new NetworkLocationRosterSnapshot(settlementId, entries.ToArray()));
    }
}
