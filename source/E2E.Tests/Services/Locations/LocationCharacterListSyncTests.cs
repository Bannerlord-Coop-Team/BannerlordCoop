using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Locations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>
/// Verifies that mutations of <see cref="Location"/>'s character list are server authoritative
/// and synced to every client
/// </summary>
public class LocationCharacterListSyncTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public LocationCharacterListSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    /// <summary>
    /// Creates a character registered on the server and all clients. Network created characters
    /// do not carry the body property range vanilla loads from xml, which building the roster
    /// entry on clients requires, so it is initialized on every client.
    /// </summary>
    private string CreateSyncedCharacter()
    {
        var characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));

                using (new AllowedThread())
                {
                    character.BodyPropertyRange = new MBBodyProperty();
                }
            });
        }

        return characterId;
    }

    /// <summary>
    /// Builds a roster entry for the given character on the server and adds it to the location
    /// through the patched mutator so the addition broadcasts to clients.
    /// </summary>
    private void AddCharacterToLocation(string locationId, string characterId)
    {
        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));

            var locationCharacter = LocationCharacterFactory.Create(
                character,
                originParty: null,
                specialItem: null,
                spawnTag: "sp_notable",
                actionSetCode: null,
                behaviorsMethodName: null,
                characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
                fixedLocation: false,
                useCivilianEquipment: true);

            location.AddCharacter(locationCharacter);
        });
    }

    [Fact]
    public void ServerAddLocationCharacter_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var locationId = TestEnvironment.CreateRegisteredObject<Location>();
        var characterId = CreateSyncedCharacter();

        // Act
        AddCharacterToLocation(locationId, characterId);

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var serverLocation));
        Assert.Single(serverLocation.GetCharacterList());

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));

            var locationCharacter = Assert.Single(location.GetCharacterList());
            Assert.True(client.ObjectManager.TryGetId(locationCharacter.Character, out var clientCharacterId));
            Assert.Equal(characterId, clientCharacterId);
        }
    }

    [Fact]
    public void ServerRemoveLocationCharacter_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var locationId = TestEnvironment.CreateRegisteredObject<Location>();
        var characterId = CreateSyncedCharacter();

        AddCharacterToLocation(locationId, characterId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.Single(location.GetCharacterList());
        }

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var location));

            var locationCharacter = Assert.Single(location.GetCharacterList());
            location.RemoveLocationCharacter(locationCharacter);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var serverLocation));
        Assert.Empty(serverLocation.GetCharacterList());

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.Empty(location.GetCharacterList());
        }
    }

    [Fact]
    public void ServerRemoveAllCharacters_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var locationId = TestEnvironment.CreateRegisteredObject<Location>();

        AddCharacterToLocation(locationId, CreateSyncedCharacter());
        AddCharacterToLocation(locationId, CreateSyncedCharacter());

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.Equal(2, location.GetCharacterList().Count());
        }

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var location));

            location.RemoveAllCharacters();
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var serverLocation));
        Assert.Empty(serverLocation.GetCharacterList());

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.Empty(location.GetCharacterList());
        }
    }

    [Fact]
    public void ServerRemoveAllCharactersPredicate_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var locationId = TestEnvironment.CreateRegisteredObject<Location>();
        var removedCharacterId = CreateSyncedCharacter();
        var keptCharacterId = CreateSyncedCharacter();

        AddCharacterToLocation(locationId, removedCharacterId);
        AddCharacterToLocation(locationId, keptCharacterId);

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(removedCharacterId, out var removedCharacter));

            location.RemoveAllCharacters(locationCharacter => locationCharacter.Character == removedCharacter);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var serverLocation));
        var serverCharacter = Assert.Single(serverLocation.GetCharacterList());
        Assert.True(server.ObjectManager.TryGetId(serverCharacter.Character, out var serverCharacterId));
        Assert.Equal(keptCharacterId, serverCharacterId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));

            var locationCharacter = Assert.Single(location.GetCharacterList());
            Assert.True(client.ObjectManager.TryGetId(locationCharacter.Character, out var clientCharacterId));
            Assert.Equal(keptCharacterId, clientCharacterId);
        }
    }

    [Fact]
    public void ClientAddLocationCharacter_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();
        var locationId = TestEnvironment.CreateRegisteredObject<Location>();

        // Act
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Location>(locationId, out var location));

            // Synced heroes are not linked to their character on clients, so the hero backed
            // character is built locally the same way the environment sets up each main hero.
            CharacterObject heroCharacter;
            using (new AllowedThread())
            {
                heroCharacter = GameObjectCreator.CreateInitializedObject<CharacterObject>();

                // Building the roster entry derives a face seed from the character's StringId;
                // a locally built (unregistered) character has none, so assign one.
                heroCharacter.StringId = "client_hero";

                heroCharacter.Culture.DefaultBattleEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
                heroCharacter.Culture.DefaultStealthEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
                heroCharacter.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item = GameObjectCreator.CreateInitializedObject<ItemObject>();

                var hero = HeroCreator.CreateSpecialHero(heroCharacter);
                heroCharacter.HeroObject = hero;
            }

            var locationCharacter = LocationCharacterFactory.Create(
                heroCharacter,
                originParty: null,
                specialItem: null,
                spawnTag: "sp_notable",
                actionSetCode: null,
                behaviorsMethodName: null,
                characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
                fixedLocation: false,
                useCivilianEquipment: true);

            location.AddCharacter(locationCharacter);

            // Client mutations of hero entries are blocked; the entry never enters the local list.
            Assert.Empty(location.GetCharacterList());
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Location>(locationId, out var serverLocation));
        Assert.Empty(serverLocation.GetCharacterList());

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var location));
            Assert.Empty(location.GetCharacterList());
        }
    }
}
