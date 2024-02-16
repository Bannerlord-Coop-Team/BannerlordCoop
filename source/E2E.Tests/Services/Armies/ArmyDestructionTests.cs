using Autofac;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Extensions;
using HarmonyLib;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class ArmyDestructionTests : IDisposable
{
    E2ETestEnvironement TestEnvironement { get; }
    public ArmyDestructionTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironement(output);
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    [Fact]
    public void ServerDestroyArmy_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironement.Server;

        var kingdom = new Kingdom();
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var serverMessageBroker = server.Container.Resolve<IMessageBroker>();

        SetupKingdom(kingdom, hero, settlement);

        // Act
        Army? army = null;
        server.Call(() =>
        {
            army = new Army(kingdom, hero.PartyBelongedTo, Army.ArmyTypes.Patrolling);
        });

        Assert.NotNull(army);

        var armyId = army!.GetStringId();

        server.Call(() =>
        {
            DisbandArmyAction.ApplyByObjectiveFinished(army);
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Army>(armyId, out var _));

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }
    }

    [Fact]
    public void ClientDestroyArmy_DoesNothing()
    {
        // Arrange
        var server = TestEnvironement.Server;
        var client1 = TestEnvironement.Clients.First();

        var kingdom = new Kingdom();
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var serverMessageBroker = server.Container.Resolve<IMessageBroker>();

        SetupKingdom(kingdom, hero, settlement);

        // Act
        string? armyId = null;
        server.Call(() =>
        {
            var army = new Army(kingdom, hero.PartyBelongedTo, Army.ArmyTypes.Patrolling);
            armyId = army!.GetStringId();
        });

        Assert.NotNull(armyId);

        // Call on client
        client1.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Army>(armyId, out var clientArmy));
            DisbandArmyAction.ApplyByObjectiveFinished(clientArmy);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Army>(armyId, out var _));

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }
    }

    private void SetupKingdom(Kingdom kingdom, Hero hero, Settlement settlement)
    {
        var settlements = (MBList<Settlement>)AccessTools.Field(typeof(Kingdom), "_settlementsCache").GetValue(kingdom)!;
        settlements.Add(settlement);

        hero.Clan.Kingdom = kingdom;
    }
}