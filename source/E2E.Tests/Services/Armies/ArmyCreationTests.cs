using Autofac;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages.Lifetime;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

[CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
public class ArmyCreationTests : IDisposable
{
    E2ETestEnvironement TestEnvironement { get; }
    public ArmyCreationTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironement(output);
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    [Fact]
    public void ServerCreateArmy_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironement.Server;

        var kingdom = new Kingdom();
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var serverMessageBroker = server.Container.Resolve<IMessageBroker>();

        SetupKingdom(kingdom, hero, settlement);

        server.ObjectManager.AddNewObject(kingdom, out string kingdomStringId);
        server.ObjectManager.AddNewObject(hero, out string heroStringId);
        server.ObjectManager.AddNewObject(hero.PartyBelongedTo, out string partyStringId);

        foreach (var client in TestEnvironement.Clients)
        {
            client.ObjectManager.AddExisting(kingdomStringId, kingdom);
            client.ObjectManager.AddExisting(heroStringId, hero);
            client.ObjectManager.AddExisting(partyStringId, hero.PartyBelongedTo);
        }

        string? newArmyStringId = null;
        serverMessageBroker.Subscribe<ArmyCreated>(payload =>
        {
            newArmyStringId = payload.What.Data.StringId;
        });

        // Act
        server.Call(() =>
        {
            kingdom.CreateArmy(hero, settlement, Army.ArmyTypes.Patrolling);
        });

        // Assert
        Assert.NotNull(newArmyStringId);

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetNonMBObject<Army>(newArmyStringId, out var newArmy));
        }
    }

    [Fact]
    public void ClientCreateArmy_DoesNothing()
    {
        // Arrange
        var server = TestEnvironement.Server;
        var client1 = TestEnvironement.Clients.First();

        var kingdom = new Kingdom();
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var serverMessageBroker = server.Container.Resolve<IMessageBroker>();

        SetupKingdom(kingdom, hero, settlement);

        server.ObjectManager.AddNewObject(kingdom, out string kingdomStringId);
        server.ObjectManager.AddNewObject(hero, out string heroStringId);
        server.ObjectManager.AddNewObject(hero.PartyBelongedTo, out string partyStringId);

        foreach (var client in TestEnvironement.Clients)
        {
            client.ObjectManager.AddExisting(kingdomStringId, kingdom);
            client.ObjectManager.AddExisting(heroStringId, hero);
            client.ObjectManager.AddExisting(partyStringId, hero.PartyBelongedTo);
        }

        string? newArmyStringId = null;
        serverMessageBroker.Subscribe<ArmyCreated>(payload =>
        {
            newArmyStringId = payload.What.Data.StringId;
        });

        // Act
        Army? clientArmy = null;
        client1.Call(() =>
        {
            kingdom.CreateArmy(hero, settlement, Army.ArmyTypes.Patrolling);
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetNonMBObject<Army>(newArmyStringId, out var _));

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.False(client.ObjectManager.TryGetNonMBObject<Army>(newArmyStringId, out var _));
        }
    }

    private void SetupKingdom(Kingdom kingdom, Hero hero, Settlement settlement)
    {
        var settlements = kingdom._settlementsCache!;
        settlements.Add(settlement);

        hero.Clan.Kingdom = kingdom;
    }
}