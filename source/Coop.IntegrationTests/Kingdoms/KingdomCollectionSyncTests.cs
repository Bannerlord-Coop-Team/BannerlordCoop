using Common.Messaging;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Kingdoms.Handlers;
using GameInterface.Services.Kingdoms.Messages.Collections;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.IntegrationTests.Kingdoms;

/// <summary>
/// E2E message-flow tests for server-authoritative Kingdom collection sync.
/// </summary>
public class KingdomCollectionSyncTests
{
    private const string KingdomId = "kingdom1";
    private const string ValueId = "value1";

    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ServerKingdom_ArmyListUpdated_Publishes_AllClients()
    {
        AssertPublishesAllClients<Army, ArmyListUpdated, NetworkUpdateArmyList>(
            instance => instance.CreateRegisteredObject<Army>(ValueId),
            (kingdom, value) => new ArmyListUpdated(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_ArmyListRemoved_Publishes_AllClients()
    {
        AssertPublishesAllClients<Army, ArmyListRemoved, NetworkRemoveArmyList>(
            instance => instance.CreateRegisteredObject<Army>(ValueId),
            (kingdom, value) => new ArmyListRemoved(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_ClanListUpdated_Publishes_AllClients()
    {
        AssertPublishesAllClients<Clan, ClanListUpdated, NetworkUpdateClanList>(
            instance => instance.CreateRegisteredObject<Clan>(ValueId),
            (kingdom, value) => new ClanListUpdated(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_ClanListRemoved_Publishes_AllClients()
    {
        AssertPublishesAllClients<Clan, ClanListRemoved, NetworkRemoveClanList>(
            instance => instance.CreateRegisteredObject<Clan>(ValueId),
            (kingdom, value) => new ClanListRemoved(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_FiefsCacheUpdated_Publishes_AllClients()
    {
        AssertPublishesAllClients<Town, FiefsCacheUpdated, NetworkUpdateFiefsCache>(
            instance => instance.CreateRegisteredObject<Town>(ValueId),
            (kingdom, value) => new FiefsCacheUpdated(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_FiefsCacheRemoved_Publishes_AllClients()
    {
        AssertPublishesAllClients<Town, FiefsCacheRemoved, NetworkRemoveFiefsCache>(
            instance => instance.CreateRegisteredObject<Town>(ValueId),
            (kingdom, value) => new FiefsCacheRemoved(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_HeroesCacheUpdated_Publishes_AllClients()
    {
        AssertPublishesAllClients<Hero, HeroesCacheUpdated, NetworkUpdateHeroesCache>(
            instance => instance.CreateRegisteredObject<Hero>(ValueId),
            (kingdom, value) => new HeroesCacheUpdated(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_HeroesCacheRemoved_Publishes_AllClients()
    {
        AssertPublishesAllClients<Hero, HeroesCacheRemoved, NetworkRemoveHeroesCache>(
            instance => instance.CreateRegisteredObject<Hero>(ValueId),
            (kingdom, value) => new HeroesCacheRemoved(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_AliveLordsCacheUpdated_Publishes_AllClients()
    {
        AssertPublishesAllClients<Hero, AliveLordsCacheUpdated, NetworkUpdateAliveLordsCache>(
            instance => instance.CreateRegisteredObject<Hero>(ValueId),
            (kingdom, value) => new AliveLordsCacheUpdated(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_AliveLordsCacheRemoved_Publishes_AllClients()
    {
        AssertPublishesAllClients<Hero, AliveLordsCacheRemoved, NetworkRemoveAliveLordsCache>(
            instance => instance.CreateRegisteredObject<Hero>(ValueId),
            (kingdom, value) => new AliveLordsCacheRemoved(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_DeadLordsCacheUpdated_Publishes_AllClients()
    {
        AssertPublishesAllClients<Hero, DeadLordsCacheUpdated, NetworkUpdateDeadLordsCache>(
            instance => instance.CreateRegisteredObject<Hero>(ValueId),
            (kingdom, value) => new DeadLordsCacheUpdated(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_DeadLordsCacheRemoved_Publishes_AllClients()
    {
        AssertPublishesAllClients<Hero, DeadLordsCacheRemoved, NetworkRemoveDeadLordsCache>(
            instance => instance.CreateRegisteredObject<Hero>(ValueId),
            (kingdom, value) => new DeadLordsCacheRemoved(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_SettlementsCacheUpdated_Publishes_AllClients()
    {
        AssertPublishesAllClients<Settlement, SettlementsCacheUpdated, NetworkUpdateSettlementsCache>(
            instance => instance.CreateRegisteredObject<Settlement>(ValueId),
            (kingdom, value) => new SettlementsCacheUpdated(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_SettlementsCacheRemoved_Publishes_AllClients()
    {
        AssertPublishesAllClients<Settlement, SettlementsCacheRemoved, NetworkRemoveSettlementsCache>(
            instance => instance.CreateRegisteredObject<Settlement>(ValueId),
            (kingdom, value) => new SettlementsCacheRemoved(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_VillagesCacheUpdated_Publishes_AllClients()
    {
        AssertPublishesAllClients<Village, VillagesCacheUpdated, NetworkUpdateVillagesCache>(
            instance => instance.CreateRegisteredObject<Village>(ValueId),
            (kingdom, value) => new VillagesCacheUpdated(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_VillagesCacheRemoved_Publishes_AllClients()
    {
        AssertPublishesAllClients<Village, VillagesCacheRemoved, NetworkRemoveVillagesCache>(
            instance => instance.CreateRegisteredObject<Village>(ValueId),
            (kingdom, value) => new VillagesCacheRemoved(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_WarPartyComponentsCacheUpdated_Publishes_AllClients()
    {
        AssertPublishesAllClients<WarPartyComponent, WarPartyComponentsCacheUpdated, NetworkUpdateWarPartyComponentsCache>(
            instance => instance.CreateRegisteredObject<LordPartyComponent>(ValueId),
            (kingdom, value) => new WarPartyComponentsCacheUpdated(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ServerKingdom_WarPartyComponentsCacheRemoved_Publishes_AllClients()
    {
        AssertPublishesAllClients<WarPartyComponent, WarPartyComponentsCacheRemoved, NetworkRemoveWarPartyComponentsCache>(
            instance => instance.CreateRegisteredObject<LordPartyComponent>(ValueId),
            (kingdom, value) => new WarPartyComponentsCacheRemoved(kingdom, value),
            message => message.KingdomId,
            message => message.ValueId);
    }

    [Fact]
    public void ApplyArmyListChange_MutatesCollection()
    {
        AssertApplyChange<Army>(
            instance => instance.CreateRegisteredObject<Army>(ValueId),
            KingdomCollectionHandler.ApplyArmyListUpdate,
            KingdomCollectionHandler.ApplyArmyListRemove,
            (kingdom, value) => kingdom._armies?.Contains(value) == true);
    }

    [Fact]
    public void ApplyClanListChange_MutatesCollection()
    {
        AssertApplyChange<Clan>(
            instance => instance.CreateRegisteredObject<Clan>(ValueId),
            KingdomCollectionHandler.ApplyClanListUpdate,
            KingdomCollectionHandler.ApplyClanListRemove,
            (kingdom, value) => kingdom._clans?.Contains(value) == true);
    }

    [Fact]
    public void ApplyFiefsCacheChange_MutatesCollection()
    {
        AssertApplyChange<Town>(
            instance => instance.CreateRegisteredObject<Town>(ValueId),
            KingdomCollectionHandler.ApplyFiefsCacheUpdate,
            KingdomCollectionHandler.ApplyFiefsCacheRemove,
            (kingdom, value) => kingdom._fiefsCache?.Contains(value) == true);
    }

    [Fact]
    public void ApplyHeroesCacheChange_MutatesCollection()
    {
        AssertApplyChange<Hero>(
            instance => instance.CreateRegisteredObject<Hero>(ValueId),
            KingdomCollectionHandler.ApplyHeroesCacheUpdate,
            KingdomCollectionHandler.ApplyHeroesCacheRemove,
            (kingdom, value) => kingdom._heroesCache?.Contains(value) == true);
    }

    [Fact]
    public void ApplyAliveLordsCacheChange_MutatesCollection()
    {
        AssertApplyChange<Hero>(
            instance => instance.CreateRegisteredObject<Hero>(ValueId),
            KingdomCollectionHandler.ApplyAliveLordsCacheUpdate,
            KingdomCollectionHandler.ApplyAliveLordsCacheRemove,
            (kingdom, value) => kingdom._aliveLordsCache?.Contains(value) == true);
    }

    [Fact]
    public void ApplyDeadLordsCacheChange_MutatesCollection()
    {
        AssertApplyChange<Hero>(
            instance => instance.CreateRegisteredObject<Hero>(ValueId),
            KingdomCollectionHandler.ApplyDeadLordsCacheUpdate,
            KingdomCollectionHandler.ApplyDeadLordsCacheRemove,
            (kingdom, value) => kingdom._deadLordsCache?.Contains(value) == true);
    }

    [Fact]
    public void ApplySettlementsCacheChange_MutatesCollection()
    {
        AssertApplyChange<Settlement>(
            instance => instance.CreateRegisteredObject<Settlement>(ValueId),
            KingdomCollectionHandler.ApplySettlementsCacheUpdate,
            KingdomCollectionHandler.ApplySettlementsCacheRemove,
            (kingdom, value) => kingdom._settlementsCache?.Contains(value) == true);
    }

    [Fact]
    public void ApplyVillagesCacheChange_MutatesCollection()
    {
        AssertApplyChange<Village>(
            instance => instance.CreateRegisteredObject<Village>(ValueId),
            KingdomCollectionHandler.ApplyVillagesCacheUpdate,
            KingdomCollectionHandler.ApplyVillagesCacheRemove,
            (kingdom, value) => kingdom._villagesCache?.Contains(value) == true);
    }

    [Fact]
    public void ApplyWarPartyComponentsCacheChange_MutatesCollection()
    {
        AssertApplyChange<WarPartyComponent>(
            instance => instance.CreateRegisteredObject<LordPartyComponent>(ValueId),
            KingdomCollectionHandler.ApplyWarPartyComponentsCacheUpdate,
            KingdomCollectionHandler.ApplyWarPartyComponentsCacheRemove,
            (kingdom, value) => kingdom._warPartyComponentsCache?.Contains(value) == true);
    }

    private void AssertPublishesAllClients<TValue, TEvent, TNetwork>(
        Func<EnvironmentInstance, TValue> createValue,
        Func<Kingdom, TValue, TEvent> createMessage,
        Func<TNetwork, string> getKingdomId,
        Func<TNetwork, string> getValueId)
        where TEvent : IMessage
        where TNetwork : IMessage
    {
        var server = TestEnvironment.Server;
        var serverKingdom = server.CreateRegisteredObject<Kingdom>(KingdomId);
        var serverValue = createValue(server);

        foreach (var client in TestEnvironment.Clients)
        {
            client.CreateRegisteredObject<Kingdom>(KingdomId);
            createValue(client);
        }

        server.SimulateMessage(this, createMessage(serverKingdom, serverValue));

        var serverMessages = server.NetworkSentMessages.GetMessages<TNetwork>();
        Assert.Single(serverMessages, message =>
            getKingdomId(message) == KingdomId &&
            getValueId(message) == ValueId);

        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            var clientMessages = client.InternalMessages.GetMessages<TNetwork>();
            Assert.Single(clientMessages, message =>
                getKingdomId(message) == KingdomId &&
                getValueId(message) == ValueId);
        }
    }

    private void AssertApplyChange<TValue>(
        Func<EnvironmentInstance, TValue> createValue,
        Action<Kingdom, TValue> update,
        Action<Kingdom, TValue> remove,
        Func<Kingdom, TValue, bool> contains)
    {
        var client = TestEnvironment.Clients.First();
        var kingdom = client.CreateRegisteredObject<Kingdom>(KingdomId);
        var value = createValue(client);

        update(kingdom, value);
        Assert.True(contains(kingdom, value));

        remove(kingdom, value);
        Assert.False(contains(kingdom, value));
    }
}
