using Common;
using Common.Util;
using Common.Messaging;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Handlers;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Heroes;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
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
        const string BattleItemId = "linen";

        server.Call(() =>
        {
            Assert.True(ModInformation.IsServer);
            var battleItem = MBObjectManager.Instance.GetObject<ItemObject>(BattleItemId);
            Assert.NotNull(battleItem);
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
            template.FirstBattleEquipment[EquipmentIndex.Body] = new EquipmentElement(battleItem);
            template.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item =
                GameObjectCreator.CreateInitializedObject<ItemObject>();

            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            serverTemplate = template;
            serverClan = clan;
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
            BattleItemId,
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
                BattleItemId,
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
    public void ClientHeroStub_IsListedOnlyOnceItsCharacterObjectArrives()
    {
        Hero? serverHero = null;
        WithoutClientHeroRecords(() =>
        {
            TestEnvironment.Server.Call(() => serverHero = new Hero());
            foreach (var client in TestEnvironment.Clients)
                client.PumpGameThread();
        });
        Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(serverHero!, out var heroId));
        var stubIds = new Dictionary<object, MBGUID>();

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var stub));
                Assert.Null(stub.CharacterObject);
                Assert.NotEqual(0u, stub.Id.InternalValue);
                stubIds[client] = stub.Id;
                // The encyclopedia and other list readers dereference CharacterObject on every listed hero
                Assert.DoesNotContain(stub, Hero.AllAliveHeroes.Concat(Hero.DeadOrDisabledHeroes));
            });
        }

        TestEnvironment.Server.Call(() =>
        {
            var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            characterObject.Culture.DefaultBattleEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item = GameObjectCreator.CreateInitializedObject<ItemObject>();
            serverHero!.SetCharacterObject(characterObject);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.NotNull(hero.CharacterObject);
                Assert.Single(Hero.AllAliveHeroes.Concat(Hero.DeadOrDisabledHeroes), listed => listed == hero);
                // Listing the stub must keep the id it got at creation
                Assert.Equal(stubIds[client], hero.Id);
            });
        }
    }

    [Fact]
    public void ClientHeroStub_StateChangeBeforeItsCharacterObject_StaysUnlistedUntilItArrives()
    {
        Hero? serverHero = null;
        TestEnvironment.Server.Call(() => serverHero = new Hero());
        Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(serverHero!, out var heroId));

        // The test bootstrap turns ChangeState into a field store, native ChangeState is what lists the hero
        var changeState = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeState));
        var bootstrap = Assert.Single(Harmony.GetPatchInfo(changeState).Prefixes, patch => patch.owner == "Coop.Testing");
        var harmony = new Harmony(bootstrap.owner);
        foreach (var client in TestEnvironment.Clients)
        {
            client.PumpGameThread();
            harmony.Unpatch(changeState, bootstrap.PatchMethod);
            try
            {
                client.SimulateMessage(this, new NetworkHeroStateChanged((int)Hero.CharacterStates.Dead, heroId));
                client.PumpGameThread();
            }
            finally
            {
                harmony.Patch(changeState, prefix: new HarmonyMethod(bootstrap.PatchMethod)
                {
                    priority = bootstrap.priority,
                    before = bootstrap.before,
                    after = bootstrap.after,
                });
            }
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var stub));
                Assert.Equal(Hero.CharacterStates.Dead, stub.HeroState);
                // Native ChangeState lists a hero that moves from alive to dead, with or without its CharacterObject
                Assert.DoesNotContain(stub, Hero.AllAliveHeroes.Concat(Hero.DeadOrDisabledHeroes));
            });
        }

        TestEnvironment.Server.Call(() =>
        {
            var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            characterObject.Culture.DefaultBattleEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item = GameObjectCreator.CreateInitializedObject<ItemObject>();
            serverHero!.SetCharacterObject(characterObject);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.NotNull(hero.CharacterObject);
                Assert.DoesNotContain(hero, Hero.AllAliveHeroes);
                Assert.Single(Hero.DeadOrDisabledHeroes, listed => listed == hero);
            });
        }
    }

    [Fact]
    public void ServerCreateSpecialHero_ListsTheHeroWithItsCharacterObjectOnClients()
    {
        Hero? serverHero = null;
        TestEnvironment.Server.Call(() =>
        {
            var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            characterObject.Culture.DefaultBattleEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item = GameObjectCreator.CreateInitializedObject<ItemObject>();
            serverHero = HeroCreator.CreateSpecialHero(characterObject);
        });
        Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(serverHero!, out var heroId));

        foreach (var client in TestEnvironment.Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.NotNull(hero.CharacterObject);
                Assert.Single(Hero.AllAliveHeroes.Concat(Hero.DeadOrDisabledHeroes), listed => listed == hero);
            });
        }
    }

    // Drops the field sync on the server, so only the initialization message can give clients the CharacterObject.
    [Fact]
    public void ServerCreateSpecialHero_WhenCharacterObjectSyncIsLost_InitializationRestoresItOnClients()
    {
        Hero? serverHero = null;
        TestEnvironment.Server.Call(() =>
        {
            var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            characterObject.Culture.DefaultBattleEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            characterObject.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item = GameObjectCreator.CreateInitializedObject<ItemObject>();
            serverHero = HeroCreator.CreateSpecialHero(characterObject);
        }, new[] { AccessTools.Method(typeof(ServerHeroFieldsHandler), "Handle", new[] { typeof(MessagePayload<CharacterObjectChanged>) }) });
        Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(serverHero!, out var heroId));

        foreach (var client in TestEnvironment.Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.NotNull(hero.CharacterObject);
                Assert.Single(Hero.AllAliveHeroes.Concat(Hero.DeadOrDisabledHeroes), listed => listed == hero);
            });
        }
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
        WithoutClientHeroRecords(() =>
        {
            server.Call(() =>
            {
                serverHero1 = new Hero();
                serverHero2 = new Hero();
            });
            foreach (var client in TestEnvironment.Clients)
                client.PumpGameThread();
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

    /// <summary>
    /// Runs <paramref name="action"/> with no Hero record in the clients' MBObjectManager, like a running campaign
    /// where SandBoxManager drops the temporary types. The harness record would hand out ids that live clients never get
    /// </summary>
    private void WithoutClientHeroRecords(Action action)
    {
        var removed = new List<(List<MBObjectManager.IObjectTypeRecord> Records, int Index, MBObjectManager.IObjectTypeRecord Record)>();
        try
        {
            foreach (var client in TestEnvironment.Clients)
            {
                // Every instance has its own MBObjectManager, so the server keeps its record
                var objectManager = client.GameInstance.MBObjectManager;
                Assert.NotSame(TestEnvironment.Server.GameInstance.MBObjectManager, objectManager);

                // The harness and the campaign each register a Hero type
                var records = objectManager.ObjectTypeRecords;
                for (var index = records.Count - 1; index >= 0; index--)
                {
                    if (records[index].ObjectClass != typeof(Hero)) continue;
                    removed.Add((records, index, records[index]));
                    records.RemoveAt(index);
                }
            }
            Assert.NotEmpty(removed);

            action();
        }
        finally
        {
            foreach (var (records, index, record) in Enumerable.Reverse(removed))
                records.Insert(index, record);
        }
    }
}
