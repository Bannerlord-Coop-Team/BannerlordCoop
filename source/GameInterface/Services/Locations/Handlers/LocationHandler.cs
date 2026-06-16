using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.Locations.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;

namespace GameInterface.Services.Locations.Handlers;

/// <summary>
/// Handler for <see cref="Location"/> roster and special item messages. Converts server-side
/// internal events into network broadcasts, and applies received broadcasts on clients.
/// </summary>
public class LocationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public LocationHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<LocationCharacterAdded>(Handle_LocationCharacterAdded);
        messageBroker.Subscribe<LocationCharacterRemoved>(Handle_LocationCharacterRemoved);
        messageBroker.Subscribe<AllLocationCharactersRemoved>(Handle_AllLocationCharactersRemoved);
        messageBroker.Subscribe<LocationSpecialItemAdded>(Handle_LocationSpecialItemAdded);
        messageBroker.Subscribe<LocationSpecialItemRemoved>(Handle_LocationSpecialItemRemoved);

        messageBroker.Subscribe<NetworkAddLocationCharacter>(Handle_NetworkAddLocationCharacter);
        messageBroker.Subscribe<NetworkRemoveLocationCharacter>(Handle_NetworkRemoveLocationCharacter);
        messageBroker.Subscribe<NetworkRemoveAllLocationCharacters>(Handle_NetworkRemoveAllLocationCharacters);
        messageBroker.Subscribe<NetworkAddLocationSpecialItem>(Handle_NetworkAddLocationSpecialItem);
        messageBroker.Subscribe<NetworkRemoveLocationSpecialItem>(Handle_NetworkRemoveLocationSpecialItem);
        messageBroker.Subscribe<NetworkLocationRosterSnapshot>(Handle_NetworkLocationRosterSnapshot);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LocationCharacterAdded>(Handle_LocationCharacterAdded);
        messageBroker.Unsubscribe<LocationCharacterRemoved>(Handle_LocationCharacterRemoved);
        messageBroker.Unsubscribe<AllLocationCharactersRemoved>(Handle_AllLocationCharactersRemoved);
        messageBroker.Unsubscribe<LocationSpecialItemAdded>(Handle_LocationSpecialItemAdded);
        messageBroker.Unsubscribe<LocationSpecialItemRemoved>(Handle_LocationSpecialItemRemoved);

        messageBroker.Unsubscribe<NetworkAddLocationCharacter>(Handle_NetworkAddLocationCharacter);
        messageBroker.Unsubscribe<NetworkRemoveLocationCharacter>(Handle_NetworkRemoveLocationCharacter);
        messageBroker.Unsubscribe<NetworkRemoveAllLocationCharacters>(Handle_NetworkRemoveAllLocationCharacters);
        messageBroker.Unsubscribe<NetworkAddLocationSpecialItem>(Handle_NetworkAddLocationSpecialItem);
        messageBroker.Unsubscribe<NetworkRemoveLocationSpecialItem>(Handle_NetworkRemoveLocationSpecialItem);
        messageBroker.Unsubscribe<NetworkLocationRosterSnapshot>(Handle_NetworkLocationRosterSnapshot);
    }

    private void Handle_LocationCharacterAdded(MessagePayload<LocationCharacterAdded> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetIdWithLogging(obj.Location, out var locationId) == false) return;
        if (objectManager.TryGetIdWithLogging(obj.Character, out var characterId) == false) return;

        // A failed lookup is logged but the entry still broadcasts without that optional field.
        string originPartyId = null;
        if (obj.OriginParty != null)
        {
            objectManager.TryGetIdWithLogging(obj.OriginParty, out originPartyId);
        }

        string specialItemId = null;
        if (obj.SpecialItem != null)
        {
            objectManager.TryGetIdWithLogging(obj.SpecialItem, out specialItemId);
        }

        var data = new LocationCharacterData(
            locationId,
            characterId,
            originPartyId,
            specialItemId,
            obj.SpawnTag,
            obj.ActionSetCode,
            obj.BehaviorsMethodName,
            obj.CharacterRelation,
            obj.FixedLocation,
            obj.UseCivilianEquipment);

        network.SendAll(new NetworkAddLocationCharacter(data));
    }

    private void Handle_LocationCharacterRemoved(MessagePayload<LocationCharacterRemoved> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetIdWithLogging(obj.Location, out var locationId) == false) return;
        if (objectManager.TryGetIdWithLogging(obj.Character, out var characterId) == false) return;

        network.SendAll(new NetworkRemoveLocationCharacter(locationId, characterId));
    }

    private void Handle_AllLocationCharactersRemoved(MessagePayload<AllLocationCharactersRemoved> payload)
    {
        if (objectManager.TryGetIdWithLogging(payload.What.Location, out var locationId) == false) return;

        network.SendAll(new NetworkRemoveAllLocationCharacters(locationId));
    }

    private void Handle_LocationSpecialItemAdded(MessagePayload<LocationSpecialItemAdded> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetIdWithLogging(obj.Location, out var locationId) == false) return;
        if (objectManager.TryGetIdWithLogging(obj.Item, out var itemId) == false) return;

        network.SendAll(new NetworkAddLocationSpecialItem(locationId, itemId));
    }

    private void Handle_LocationSpecialItemRemoved(MessagePayload<LocationSpecialItemRemoved> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetIdWithLogging(obj.Location, out var locationId) == false) return;
        if (objectManager.TryGetIdWithLogging(obj.Item, out var itemId) == false) return;

        network.SendAll(new NetworkRemoveLocationSpecialItem(locationId, itemId));
    }

    private void Handle_NetworkAddLocationCharacter(MessagePayload<NetworkAddLocationCharacter> payload)
    {
        var data = payload.What.Data;

        // Creating the LocationCharacter builds AgentData and FaceGen monsters,
        // which must run on the main thread like all other game object construction.
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (TryCreateFromData(data, out var location, out var locationCharacter) == false) return;

                LocationCharacterListPatches.AddLocationCharacter(location, locationCharacter);
            }
        });
    }

    private void Handle_NetworkRemoveLocationCharacter(MessagePayload<NetworkRemoveLocationCharacter> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObjectWithLogging(obj.LocationId, out Location location) == false) return;
        if (objectManager.TryGetObjectWithLogging(obj.CharacterId, out CharacterObject character) == false) return;

        LocationCharacterListPatches.RemoveLocationCharacter(location, character);
    }

    private void Handle_NetworkRemoveAllLocationCharacters(MessagePayload<NetworkRemoveAllLocationCharacters> payload)
    {
        if (objectManager.TryGetObjectWithLogging(payload.What.LocationId, out Location location) == false) return;

        LocationCharacterListPatches.RemoveAllLocationCharacters(location);
    }

    private void Handle_NetworkAddLocationSpecialItem(MessagePayload<NetworkAddLocationSpecialItem> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObjectWithLogging(obj.LocationId, out Location location) == false) return;
        if (objectManager.TryGetObjectWithLogging(obj.ItemId, out ItemObject item) == false) return;

        LocationSpecialItemsPatches.AddSpecialItem(location, item);
    }

    private void Handle_NetworkRemoveLocationSpecialItem(MessagePayload<NetworkRemoveLocationSpecialItem> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObjectWithLogging(obj.LocationId, out Location location) == false) return;
        if (objectManager.TryGetObjectWithLogging(obj.ItemId, out ItemObject item) == false) return;

        LocationSpecialItemsPatches.RemoveSpecialItem(location, item);
    }

    private void Handle_NetworkLocationRosterSnapshot(MessagePayload<NetworkLocationRosterSnapshot> payload)
    {
        // The server is the snapshot's source of truth and never applies one.
        if (ModInformation.IsServer) return;

        var obj = payload.What;

        if (objectManager.TryGetObjectWithLogging(obj.SettlementId, out Settlement settlement) == false) return;

        var locationComplex = settlement.LocationComplex;
        if (locationComplex == null) return;

        var entries = obj.Entries ?? Array.Empty<LocationCharacterData>();

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                ReconcileSettlementRosters(locationComplex, entries);
            }
        });
    }

    /// <summary>
    /// Diffs each location's synced entries against the snapshot and applies only the difference.
    /// A wholesale clear-and-rebuild would respawn agents for entries a running mission already
    /// spawned and would wipe locally generated ambient entries, so only synced entries are managed.
    /// </summary>
    private void ReconcileSettlementRosters(LocationComplex locationComplex, LocationCharacterData[] entries)
    {
        var entriesByLocation = entries
            .Where(entry => entry?.LocationId != null)
            .GroupBy(entry => entry.LocationId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var location in locationComplex.GetListOfLocations())
        {
            if (objectManager.TryGetId(location, out var locationId) == false) continue;

            entriesByLocation.TryGetValue(locationId, out var desiredEntries);

            var desiredByCharacter = new Dictionary<string, Queue<LocationCharacterData>>();
            foreach (var desired in desiredEntries ?? Enumerable.Empty<LocationCharacterData>())
            {
                if (desiredByCharacter.TryGetValue(desired.CharacterId, out var queue) == false)
                {
                    queue = new Queue<LocationCharacterData>();
                    desiredByCharacter[desired.CharacterId] = queue;
                }
                queue.Enqueue(desired);
            }

            var currentSynced = location.GetCharacterList()?.Where(SyncedLocationCharacters.IsSynced).ToList()
                ?? new List<LocationCharacter>();

            foreach (var current in currentSynced)
            {
                if (current.Character != null &&
                    objectManager.TryGetId(current.Character, out var characterId) &&
                    desiredByCharacter.TryGetValue(characterId, out var queue) &&
                    queue.Count > 0)
                {
                    // Entry already present; consume the matching snapshot entry.
                    queue.Dequeue();
                    continue;
                }

                LocationCharacterListPatches.RemoveEntry(location, current);
            }

            foreach (var missing in desiredByCharacter.Values.SelectMany(queue => queue))
            {
                if (TryCreateFromData(missing, out var entryLocation, out var locationCharacter) == false) continue;

                LocationCharacterListPatches.AddEntry(entryLocation, locationCharacter);
            }
        }
    }

    private bool TryCreateFromData(LocationCharacterData data, out Location location, out LocationCharacter locationCharacter)
    {
        locationCharacter = null;

        if (objectManager.TryGetObjectWithLogging(data.LocationId, out location) == false) return false;
        if (objectManager.TryGetObjectWithLogging(data.CharacterId, out CharacterObject character) == false) return false;

        // Players are represented inside interiors by the P2P mission layer (their controllable agent),
        // not by the server-authoritative location roster. Spawning a roster copy of a player hero
        // produces a second, frozen duplicate alongside the P2P agent — so never roster-spawn one.
        if (character.IsHero && character.HeroObject != null && character.HeroObject.IsPlayerHero())
        {
            Logger.Debug("Skipping location roster spawn for player hero {Hero} — represented via P2P", character.StringId);
            return false;
        }

        // A failed lookup is logged but still degrades gracefully: the entry is created with a
        // SimpleAgentOrigin / without the item rather than being dropped.
        MobileParty originParty = null;
        if (string.IsNullOrEmpty(data.OriginPartyId) == false)
        {
            objectManager.TryGetObjectWithLogging(data.OriginPartyId, out originParty);
        }

        ItemObject specialItem = null;
        if (string.IsNullOrEmpty(data.SpecialItemId) == false)
        {
            objectManager.TryGetObjectWithLogging(data.SpecialItemId, out specialItem);
        }

        locationCharacter = LocationCharacterFactory.Create(
            character,
            originParty,
            specialItem,
            data.SpawnTag,
            data.ActionSetCode,
            data.BehaviorsMethodName,
            data.CharacterRelation,
            data.FixedLocation,
            data.UseCivilianEquipment);

        return true;
    }
}
