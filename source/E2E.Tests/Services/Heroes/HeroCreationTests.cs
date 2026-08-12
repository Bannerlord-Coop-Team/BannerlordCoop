using Common;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Heroes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class HeroCreationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public HeroCreationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateHero_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        

        // Act
        Hero? serverHero = null;
        server.Call(() =>
        {
            var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            // Required: SetupMainHero also initializes these to avoid NullReferenceException in SetInitialValuesFromCharacter
            characterObject.Culture.DefaultBattleEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item = GameObjectCreator.CreateInitializedObject<ItemObject>();

            var hero = HeroCreator.CreateSpecialHero(characterObject);

            hero.BornSettlement = Settlement.GetFirst;
            serverHero = hero;

            hero.SetName(new TextObject("Test Name"), new TextObject("Name"));
        });

        // Assert
        var newHeroStringId = serverHero?.StringId;
        Assert.NotNull(newHeroStringId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(newHeroStringId, out var newHero));

            Assert.Equal(serverHero?.FirstName.Value, newHero.FirstName.Value);
        }
    }

    [Fact]
    public void ServerCreateConfiguredMinorFactionLord_UsesFullTemplateAndSyncs()
    {
        var server = TestEnvironment.Server;
        Hero? serverHero = null;
        CharacterObject? serverTemplate = null;
        Clan? serverClan = null;
        string? expectedBattleItemId = null;

        server.Call(() =>
        {
            Assert.True(ModInformation.IsServer);
            var template = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            using (new AllowedThread())
                template.StringId = ConfiguredMinorFactionHeroSpawner.ConfiguredLordPrefix + "test";
            template._basicName = new TextObject("Hasted");
            template._occupation = Occupation.Lord;
            template.IsTemplate = true;
            template.Level = 27;
            template.Culture.DefaultBattleEquipmentRoster =
                GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            template.Culture.DefaultStealthEquipmentRoster =
                GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            template.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item =
                GameObjectCreator.CreateInitializedObject<ItemObject>();

            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            serverTemplate = template;
            serverClan = clan;
            expectedBattleItemId = template.FirstBattleEquipment[EquipmentIndex.Body].Item.StringId;
            serverHero = ConfiguredMinorFactionHeroSpawner.CreateHeroFromTemplate(template, clan);
        });

        Assert.NotNull(serverHero);
        Assert.Same(serverTemplate, serverHero.Template);
        Assert.Same(serverTemplate, serverHero.CharacterObject.OriginalCharacter);
        Assert.Same(serverClan, serverHero.Clan);
        Assert.Equal("Hasted", serverHero.Name.ToString());
        Assert.Equal("Hasted", serverHero.FirstName.ToString());
        Assert.Equal(27, serverHero.Level);
        Assert.Equal(Occupation.Lord, serverHero.Occupation);
        Assert.Same(serverTemplate!.Culture, serverHero.Culture);
        Assert.Equal(
            expectedBattleItemId,
            serverHero.BattleEquipment[EquipmentIndex.Body].Item.StringId);
        Assert.True(serverHero.IsMinorFactionHero);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(serverHero.StringId, out var clientHero));
            Assert.Equal("Hasted", clientHero.Name.ToString());
            Assert.Equal("Hasted", clientHero.FirstName.ToString());
            Assert.Equal(27, clientHero.Level);
            Assert.Equal(Occupation.Lord, clientHero.Occupation);
            Assert.Equal(serverTemplate.Culture.StringId, clientHero.Culture.StringId);
            Assert.Equal(
                expectedBattleItemId,
                clientHero.BattleEquipment[EquipmentIndex.Body].Item.StringId);
            Assert.True(clientHero.IsMinorFactionHero);
        }
    }

    [Fact]
    public void ServerInitialCoopTeamFill_CreatesEveryEnabledTemplateAndNeverRefills()
    {
        TestEnvironment.Server.Call(() =>
        {
            static CharacterObject CreateTemplate(string id, string name, bool enabled)
            {
                var template = GameObjectCreator.CreateInitializedObject<CharacterObject>();
                using (new AllowedThread())
                    template.StringId = id;
                template._basicName = new TextObject(name);
                template._occupation = Occupation.Lord;
                template.IsTemplate = enabled;
                template.Culture.DefaultBattleEquipmentRoster =
                    GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
                template.Culture.DefaultStealthEquipmentRoster =
                    GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
                template.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item =
                    GameObjectCreator.CreateInitializedObject<ItemObject>();
                return template;
            }

            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            using (new AllowedThread())
                clan.StringId = ConfiguredMinorFactionHeroSpawner.CoopTeamClanId;
            clan._minorFactionCharacterTemplates = new MBList<CharacterObject>
            {
                CreateTemplate(
                    ConfiguredMinorFactionHeroSpawner.CoopTeamLeaderTemplateId,
                    "AnotherJoke",
                    enabled: true),
                CreateTemplate("coop_team_lord_disabled", "Disabled", enabled: false),
                CreateTemplate("coop_team_lord_2", "Lord 2", enabled: true),
                CreateTemplate("coop_team_lord_3", "Lord 3", enabled: true),
                CreateTemplate("coop_team_lord_4", "Lord 4", enabled: true),
                CreateTemplate("coop_team_lord_added_later", "Lord 5", enabled: true),
            };

            Assert.False(ConfiguredMinorFactionHeroSpawner.SpawnMinorFactionHeroesPrefix(
                clan,
                firstTime: true));

            Assert.Equal(
                new[]
                {
                    ConfiguredMinorFactionHeroSpawner.CoopTeamLeaderTemplateId,
                    "coop_team_lord_2",
                    "coop_team_lord_3",
                    "coop_team_lord_4",
                    "coop_team_lord_added_later",
                },
                clan.AliveLords.Select(hero => hero.Template.StringId));
            Assert.Equal(
                ConfiguredMinorFactionHeroSpawner.CoopTeamLeaderTemplateId,
                clan.Leader.Template.StringId);

            Assert.False(ConfiguredMinorFactionHeroSpawner.SpawnMinorFactionHeroesPrefix(
                clan,
                firstTime: false));
            Assert.Equal(5, clan.AliveLords.Count);
        });
    }

    [Fact]
    public void ServerCreateBareHero_PreservesDefaultHealthOnClients()
    {
        Hero? serverHero = null;

        TestEnvironment.Server.Call(() => serverHero = new Hero());
        Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(serverHero!, out var heroId));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var clientHero));
            Assert.Equal(1, clientHero._health);
        }
    }

    [Fact]
    public void ServerCreateHeroes_ClientHeroesGetUniqueNonZeroIds()
    {
        // Arrange
        var server = TestEnvironment.Server;

        Hero? serverHero1 = null;
        Hero? serverHero2 = null;

        // Act
        server.Call(() =>
        {
            serverHero1 = new Hero();
            serverHero2 = new Hero();
        });

        Assert.True(server.ObjectManager.TryGetId(serverHero1!, out var heroId1));
        Assert.True(server.ObjectManager.TryGetId(serverHero2!, out var heroId2));

        // Assert
        // MBObjectBase.GetHashCode is Id-based. Client-created heroes used to skip vanilla
        // AddHero's MBGUID assignment (OnHeroAdded was called directly), leaving Id == 0 for
        // every synced hero: all of them collided in any Id-hashed dictionary, and a later Id
        // assignment would strand existing entries under the stale hash - the defect that
        // leaked dead-party nameplates (see MobilePartyRegistry.OnClientCreated).
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId1, out var clientHero1));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId2, out var clientHero2));

            Assert.NotEqual(0u, clientHero1.Id.InternalValue);
            Assert.NotEqual(0u, clientHero2.Id.InternalValue);
            Assert.NotEqual(clientHero1.Id, clientHero2.Id);
        }
    }

    [Fact]
    public void ClientCreateHero_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();
        
        // Act
        Hero? clientHero = null;
        client1.Call(() =>
        {
            var hero = new Hero();

            hero.BornSettlement = Settlement.GetFirst;
            clientHero = hero;
        });

        var newHeroStringId = clientHero?.StringId;

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Hero>(newHeroStringId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Hero>(newHeroStringId, out var _));
        }
    }
}
