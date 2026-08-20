using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Tournaments;

[Collection(nameof(TournamentCombatInfrastructureCollection))]
public class TournamentWorldItemOrderingTests : MissionTestEnvironment
{
    public TournamentWorldItemOrderingTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RuntimeWorldItemBeforeDirectDrop_PreservesCanonicalItem()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                MissionEquipment equipment = CreateEquipment(weapon);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var controller = observer.Resolve<CoopTournamentController>();
                InvokeReconcileRuntimeEquipment(controller, agent);
                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity runtimeItem = spawner.AddPresent(weapon);
                worldItemRegistry.Register(worldItemId, runtimeItem);

                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));

                Assert.Equal(0, AgentEquipmentShim.GetDropCount(agent));
                Assert.Equal(0, spawner.SpawnCount);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registeredItem));
                Assert.Same(runtimeItem, registeredItem);
                Assert.Single(worldItemRegistry.GetAll());
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void RuntimeWorldItemBeforeDirectDrop_ReconcilesPopulatedSlotWithoutDuplicate()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                MissionEquipment equipment = CreateEquipment(weapon);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity runtimeItem = spawner.AddPresent(weapon);
                worldItemRegistry.Register(worldItemId, runtimeItem);

                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));

                Assert.Equal(0, AgentEquipmentShim.GetDropCount(agent));
                Assert.Equal(1, AgentEquipmentShim.GetRemoveCount(agent));
                Assert.Equal(0, spawner.SpawnCount);
                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registeredItem));
                Assert.Same(runtimeItem, registeredItem);
                Assert.Single(worldItemRegistry.GetAll());
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PeerPrecededDrop_BindsObservedItemWithoutNativeLookup()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity observedItem = spawner.AddPresent(weapon);
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.ExtraWeaponSlot,
                        weapon,
                        observedItem));
                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        agentId,
                        worldItemId,
                        itemObjectId,
                        weapon,
                        EquipmentIndex.ExtraWeaponSlot));

                Assert.Equal(0, AgentEquipmentShim.GetWeaponEntityLookupCount(agent));
                Assert.Equal(0, AgentEquipmentShim.GetDropCount(agent));
                Assert.Equal(0, AgentEquipmentShim.GetRemoveCount(agent));
                Assert.Equal(0, spawner.SpawnCount);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registeredItem));
                Assert.Same(observedItem, registeredItem);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PeerPrecededDrop_AppliesAuthoritativeEquipmentState()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity observedItem = spawner.AddPresent(weapon);
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.ExtraWeaponSlot,
                        weapon,
                        observedItem));
                var authoritativeEquipment = new AgentEquipmentData(
                    EquipmentIndex.None,
                    EquipmentIndex.None,
                    0);
                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        agentId,
                        worldItemId,
                        itemObjectId,
                        weapon,
                        EquipmentIndex.ExtraWeaponSlot,
                        currentEquipment: authoritativeEquipment));

                Assert.True(agentRegistry.TryGetAgentInfo(agentId, out CoopAgentInfo agentInfo));
                Assert.True(agentInfo.TryGetAuthoritativeEquipment(out AgentEquipmentData recordedEquipment));
                Assert.Equal(authoritativeEquipment, recordedEquipment);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void LocalPartialPickup_OverridesLaterStaleDropAmount()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon resultingWeapon = CreateWeapon(itemObject);
                resultingWeapon.Amount = 3;
                AgentEquipmentShim.Track(picker, CreateEquipment(resultingWeapon));

                Guid pickerId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                var spawner = new RecordingWorldItemSpawner();
                MissionWeapon remainingWeapon = CreateWeapon(itemObject);
                remainingWeapon.Amount = 2;
                SpawnedItemEntity worldItem = spawner.AddPresent(remainingWeapon);
                worldItemRegistry.Register(worldItemId, worldItem);
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    new WeaponPickedup(
                        picker,
                        worldItem,
                        EquipmentIndex.Weapon0,
                        itemObject,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        5,
                        3,
                        2,
                        false,
                        resultingWeapon));

                MissionWeapon staleWeapon = CreateWeapon(itemObject);
                staleWeapon.Amount = 5;
                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        pickerId,
                        worldItemId,
                        itemObjectId,
                        staleWeapon,
                        isCatchUp: true));

                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity retained));
                Assert.Equal(2, retained.WeaponCopy.Amount);
                Assert.Equal(0, spawner.SpawnCount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ModifiedPickupThenDrop_UsesRegisteredResultingModifierIdentity()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            var modifier = new ItemModifier();
            string modifierId = "weapon_pickup_modifier_" + Guid.NewGuid().ToString("N");
            Assert.True(objectManager.AddExisting(modifierId, modifier));
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                Assert.True(agentRegistry.TryRegisterAgent("observer", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                MissionWeapon worldWeapon = CreateWeapon(itemObject);
                SpawnedItemEntity worldItem = spawner.AddPresent(worldWeapon);
                worldItemRegistry.Register(worldItemId, worldItem);
                using var messageBroker = new MessageBroker();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    network,
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        1,
                        1,
                        0,
                        true,
                        itemObjectId,
                        modifierId,
                        null,
                        1));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                MissionWeapon resultingWeapon = equipment[EquipmentIndex.Weapon0];
                Assert.Same(modifier, resultingWeapon.ItemModifier);
                network.NetworkSentMessages.Clear();
                SpawnedItemEntity droppedItem = spawner.AddPresent(resultingWeapon);
                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        resultingWeapon,
                        droppedItem));

                NetworkWeaponDropped sent = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
                Assert.Equal(modifierId, sent.ItemModifierId);
            }
            finally
            {
                objectManager.Remove(itemObject);
                objectManager.Remove(modifier);
            }
        });
    }

    [Fact]
    public void RegisteredFullPickup_BroadcastsBeforeRegistryRetirement()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(picker, CreateEquipment(weapon));

                Guid pickerId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity worldItem = spawner.AddPresent(weapon);
                worldItem.Id = new MissionObjectId(78, true);
                worldItemRegistry.Register(worldItemId, worldItem);
                using var messageBroker = new MessageBroker();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    network,
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    new WeaponPickedup(
                        picker,
                        worldItem,
                        EquipmentIndex.Weapon0,
                        itemObject,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        1,
                        1,
                        0,
                        true,
                        weapon));

                NetworkWeaponPickedup sent = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.Equal(worldItemId, sent.WorldItemId);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out _));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void UncorrelatedRuntimePickup_BroadcastsWithoutStableWorldItemIdentity()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(picker, CreateEquipment(weapon));

                Guid pickerId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity worldItem = spawner.AddPresent(weapon);
                worldItem.Id = new MissionObjectId(79, true);
                using var messageBroker = new MessageBroker();
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    network,
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    new WeaponPickedup(
                        picker,
                        worldItem,
                        EquipmentIndex.Weapon0,
                        itemObject,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        1,
                        1,
                        0,
                        true,
                        weapon));

                NetworkWeaponPickedup sent = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.Equal(Guid.Empty, sent.WorldItemId);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PickupBeforeObservedDropIdentity_DefersBroadcastAndRetiresCanonicalItem()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent dropper = ObjectHelper.SkipConstructor<Agent>();
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(dropper, CreateEquipment(default));
                AgentEquipmentShim.Track(picker, CreateEquipment(weapon));

                Guid dropperId = Guid.NewGuid();
                Guid pickerId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                Assert.True(agentRegistry.TryRegisterAgent("fighter", dropperId, dropper));
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity observedItem = spawner.AddPresent(weapon);
                observedItem.Id = new MissionObjectId(77, true);
                using var messageBroker = new MessageBroker();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    network,
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        dropper,
                        EquipmentIndex.Weapon0,
                        weapon,
                        observedItem));
                spawner.TryRemove(observedItem);
                messageBroker.Publish(
                    this,
                    new WeaponPickedup(
                        picker,
                        observedItem,
                        EquipmentIndex.Weapon0,
                        itemObject,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        1,
                        1,
                        0,
                        true,
                        weapon));

                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                ExpirePendingObservedDrop(dropHandler, dropperId, EquipmentIndex.Weapon0);
                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        dropperId,
                        worldItemId,
                        itemObjectId,
                        weapon));

                NetworkWeaponPickedup sent = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.Equal(worldItemId, sent.WorldItemId);
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.Equal(0, spawner.SpawnCount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PeerPrecededDrop_SelectsMatchingObservationAndDiscardsOlderMismatch()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject mismatchedItemObject = RegisterItem(objectManager, out _);
            ItemObject canonicalItemObject = RegisterItem(objectManager, out string canonicalItemId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon mismatchedWeapon = CreateWeapon(mismatchedItemObject);
                MissionWeapon canonicalWeapon = CreateWeapon(canonicalItemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity mismatchedObservedItem = spawner.AddPresent(mismatchedWeapon);
                SpawnedItemEntity matchingObservedItem = spawner.AddPresent(canonicalWeapon);
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        mismatchedWeapon,
                        mismatchedObservedItem));
                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        canonicalWeapon,
                        matchingObservedItem));
                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        agentId,
                        worldItemId,
                        canonicalItemId,
                        canonicalWeapon));

                Assert.False(spawner.IsPresent(mismatchedObservedItem));
                Assert.True(spawner.IsPresent(matchingObservedItem));
                Assert.Equal(0, spawner.SpawnCount);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registeredItem));
                Assert.Same(matchingObservedItem, registeredItem);
            }
            finally
            {
                objectManager.Remove(mismatchedItemObject);
                objectManager.Remove(canonicalItemObject);
            }
        });
    }

    [Fact]
    public void PeerPrecededDrops_RetainLaterObservationForItsCanonicalMessage()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject firstItemObject = RegisterItem(objectManager, out string firstItemId);
            ItemObject secondItemObject = RegisterItem(objectManager, out string secondItemId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon firstWeapon = CreateWeapon(firstItemObject);
                MissionWeapon secondWeapon = CreateWeapon(secondItemObject);
                AgentEquipmentShim.Track(agent, CreateEquipment(default));

                Guid agentId = Guid.NewGuid();
                Guid firstWorldItemId = Guid.NewGuid();
                Guid secondWorldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity firstObserved = spawner.AddPresent(firstWeapon);
                SpawnedItemEntity secondObserved = spawner.AddPresent(secondWeapon);
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    new WeaponDropped(agent, EquipmentIndex.Weapon0, firstWeapon, firstObserved));
                messageBroker.Publish(
                    this,
                    new WeaponDropped(agent, EquipmentIndex.Weapon0, secondWeapon, secondObserved));
                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, firstWorldItemId, firstItemId, firstWeapon));
                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, secondWorldItemId, secondItemId, secondWeapon));

                Assert.True(worldItemRegistry.TryGet(firstWorldItemId, out SpawnedItemEntity firstRegistered));
                Assert.True(worldItemRegistry.TryGet(secondWorldItemId, out SpawnedItemEntity secondRegistered));
                Assert.Same(firstObserved, firstRegistered);
                Assert.Same(secondObserved, secondRegistered);
                Assert.Equal(0, spawner.SpawnCount);
            }
            finally
            {
                objectManager.Remove(firstItemObject);
                objectManager.Remove(secondItemObject);
            }
        });
    }

    [Fact]
    public void ObservedDropTimeout_WithLocalReplacement_RestoresObservedWeapon()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject authoritativeItem = RegisterItem(objectManager, out _);
            ItemObject replacementItem = RegisterItem(objectManager, out _);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon authoritativeWeapon = CreateWeapon(authoritativeItem);
                MissionEquipment equipment = CreateEquipment(CreateWeapon(replacementItem));
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity observedItem = spawner.AddPresent(authoritativeWeapon);
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        authoritativeWeapon,
                        observedItem));
                ExpirePendingObservedDrop(handler, agentId, EquipmentIndex.Weapon0);

                Assert.False(spawner.IsPresent(observedItem));
                Assert.Same(authoritativeItem, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(1, AgentEquipmentShim.GetRemoveCount(agent));
                Assert.Equal(1, AgentEquipmentShim.GetEquipCount(agent));
            }
            finally
            {
                objectManager.Remove(authoritativeItem);
                objectManager.Remove(replacementItem);
            }
        });
    }

    [Fact]
    public void ObservedDropTimeout_AfterAuthoritativePickup_PreservesPopulatedSlot()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject observedItemObject = RegisterItem(objectManager, out _);
            ItemObject replacementItemObject = RegisterItem(objectManager, out string replacementItemId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon observedWeapon = CreateWeapon(observedItemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));
                var currentEquipment = new AgentEquipmentData(
                    EquipmentIndex.None,
                    EquipmentIndex.None,
                    0);

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity observedItem = spawner.AddPresent(observedWeapon);
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    observer.Resolve<IBattleNetwork>(),
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        observedWeapon,
                        observedItem));
                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        Guid.Empty,
                        replacementItemId,
                        null,
                        null,
                        currentEquipment,
                        0,
                        1,
                        1,
                        0,
                        true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                ExpirePendingObservedDrop(handler, agentId, EquipmentIndex.Weapon0);

                Assert.False(spawner.IsPresent(observedItem));
                Assert.Same(replacementItemObject, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(0, AgentEquipmentShim.GetRemoveCount(agent));
                Assert.Equal(1, AgentEquipmentShim.GetEquipCount(agent));
            }
            finally
            {
                objectManager.Remove(observedItemObject);
                objectManager.Remove(replacementItemObject);
            }
        });
    }

    [Fact]
    public void ObservedDropTimeout_AfterRejectedPickup_RestoresObservedWeapon()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject observedItemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon observedWeapon = CreateWeapon(observedItemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity observedItem = spawner.AddPresent(observedWeapon);
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    observer.Resolve<IBattleNetwork>(),
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        observedWeapon,
                        observedItem));
                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        Guid.Empty,
                        "missing_weapon_pickup_item",
                        null,
                        null,
                        default,
                        0,
                        0,
                        1,
                        0,
                        true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                ExpirePendingObservedDrop(handler, agentId, EquipmentIndex.Weapon0);

                Assert.False(spawner.IsPresent(observedItem));
                Assert.Same(observedItemObject, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(1, AgentEquipmentShim.GetEquipCount(agent));
            }
            finally
            {
                objectManager.Remove(observedItemObject);
            }
        });
    }

    [Fact]
    public void MatchingSlot_DropsOnceAndDuplicateIsIdempotent()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                MissionEquipment equipment = CreateEquipment(weapon);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                NetworkWeaponDropped message = CreateDropMessage(
                    agentId,
                    worldItemId,
                    itemObjectId,
                    weapon);

                messageBroker.Publish(this, message);
                messageBroker.Publish(this, message);

                Assert.Equal(1, AgentEquipmentShim.GetDropCount(agent));
                Assert.Equal(0, AgentEquipmentShim.GetWeaponEntityLookupCount(agent));
                Assert.Equal(1, spawner.SpawnCount);
                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.Single(worldItemRegistry.GetAll());
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void EmptySlot_SpawnsCanonicalItemWithoutNativeLookup()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));

                Assert.Equal(0, AgentEquipmentShim.GetDropCount(agent));
                Assert.Equal(0, AgentEquipmentShim.GetRemoveCount(agent));
                Assert.Equal(0, AgentEquipmentShim.GetWeaponEntityLookupCount(agent));
                Assert.Equal(1, spawner.SpawnCount);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registeredItem));
                Assert.Same(spawner.LastSpawnedItem, registeredItem);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void MismatchedSlot_ClearsSlotAndSpawnsCanonicalItem()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject canonicalItem = RegisterItem(objectManager, out string canonicalItemId);
            ItemObject mismatchedItem = RegisterItem(objectManager, out _);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon canonicalWeapon = CreateWeapon(canonicalItem);
                MissionEquipment equipment = CreateEquipment(CreateWeapon(mismatchedItem));
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        agentId,
                        worldItemId,
                        canonicalItemId,
                        canonicalWeapon));

                Assert.Equal(0, AgentEquipmentShim.GetDropCount(agent));
                Assert.Equal(1, AgentEquipmentShim.GetRemoveCount(agent));
                Assert.Equal(0, AgentEquipmentShim.GetWeaponEntityLookupCount(agent));
                Assert.Equal(1, spawner.SpawnCount);
                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registeredItem));
                Assert.Same(spawner.LastSpawnedItem, registeredItem);
                Assert.Same(canonicalItem, registeredItem.WeaponCopy.Item);
            }
            finally
            {
                objectManager.Remove(canonicalItem);
                objectManager.Remove(mismatchedItem);
            }
        });
    }

    [Fact]
    public void CatchUp_ReplaysActiveWorldItemWithoutEquipmentMutation()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var handler = new WeaponDropHandler(
                    agentRegistry,
                    worldItemRegistry,
                    messageBroker,
                    network,
                    objectManager,
                    spawner);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));
                network.NetworkSentMessages.Clear();

                handler.CatchUpJoiner("joiner");

                NetworkWeaponDropped catchUp = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
                Assert.True(catchUp.IsCatchUp);
                Assert.Equal(worldItemId, catchUp.WorldItemId);
                Assert.Equal(worldItemId, catchUp.DropId);

                Agent joiningAgent = ObjectHelper.SkipConstructor<Agent>();
                MissionEquipment joiningEquipment = CreateEquipment(weapon);
                AgentEquipmentShim.Track(joiningAgent, joiningEquipment);
                Assert.True(agentRegistry.RemoveAgent(agentId));
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, joiningAgent));

                var missingWorldItemRegistry = new NetworkWorldItemRegistry();
                var missingItemSpawner = new RecordingWorldItemSpawner();
                using (var missingItemBroker = new MessageBroker())
                using (var missingItemHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    missingWorldItemRegistry,
                    objectManager,
                    missingItemSpawner,
                    missingItemBroker))
                {
                    missingItemBroker.Publish(this, catchUp);

                    Assert.Equal(1, missingItemSpawner.SpawnCount);
                    Assert.True(missingWorldItemRegistry.TryGet(
                        worldItemId,
                        out SpawnedItemEntity spawnedCatchUpItem));
                    Assert.Same(missingItemSpawner.LastSpawnedItem, spawnedCatchUpItem);
                }

                var registeredWorldItemRegistry = new NetworkWorldItemRegistry();
                var registeredItemSpawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity registeredCatchUpItem = registeredItemSpawner.AddPresent(weapon);
                registeredWorldItemRegistry.Register(worldItemId, registeredCatchUpItem);
                using (var registeredItemBroker = new MessageBroker())
                using (var registeredItemHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    registeredWorldItemRegistry,
                    objectManager,
                    registeredItemSpawner,
                    registeredItemBroker))
                {
                    registeredItemBroker.Publish(this, catchUp);

                    Assert.Equal(0, registeredItemSpawner.SpawnCount);
                    Assert.True(registeredWorldItemRegistry.TryGet(
                        worldItemId,
                        out SpawnedItemEntity retainedCatchUpItem));
                    Assert.Same(registeredCatchUpItem, retainedCatchUpItem);
                }

                var retiredWorldItemRegistry = new NetworkWorldItemRegistry();
                var retiredItemSpawner = new RecordingWorldItemSpawner();
                using (var retiredItemBroker = new MessageBroker())
                using (var retiredItemHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    retiredWorldItemRegistry,
                    objectManager,
                    retiredItemSpawner,
                    retiredItemBroker))
                {
                    retiredItemBroker.Publish(
                        this,
                        new WeaponPickupApplied(agentId, EquipmentIndex.Weapon0, worldItemId, 0, true));
                    retiredItemBroker.Publish(this, catchUp);

                    Assert.Equal(0, retiredItemSpawner.SpawnCount);
                    Assert.False(retiredWorldItemRegistry.TryGet(worldItemId, out _));
                }

                Assert.Same(itemObject, joiningEquipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(0, AgentEquipmentShim.GetDropCount(joiningAgent));
                Assert.Equal(0, AgentEquipmentShim.GetRemoveCount(joiningAgent));
                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.Equal(0, AgentEquipmentShim.GetDropCount(agent));
                Assert.Equal(0, AgentEquipmentShim.GetRemoveCount(agent));
                Assert.Equal(1, spawner.SpawnCount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PickupBeforeCatchUp_AppliesDetachedAndPreventsWorldItemRespawn()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    observer.Resolve<IBattleNetwork>(),
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        1,
                        1,
                        0,
                        true));
                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        agentId,
                        worldItemId,
                        itemObjectId,
                        weapon,
                        isCatchUp: true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(itemObject, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(1, AgentEquipmentShim.GetEquipCount(agent));
                Assert.Equal(0, spawner.SpawnCount);
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialPickupBeforeDrop_PreservesRemainingWorldItem(bool isCatchUp)
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon staleWeapon = CreateWeapon(itemObject);
                staleWeapon.Amount = 5;
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    observer.Resolve<IBattleNetwork>(),
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        5,
                        3,
                        2,
                        false));
                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        agentId,
                        worldItemId,
                        itemObjectId,
                        staleWeapon,
                        isCatchUp: isCatchUp));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Equal(1, spawner.SpawnCount);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity remainingItem));
                Assert.Equal(2, remainingItem.WeaponCopy.Amount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void CatchUpBeforePartialPickup_PreservesAuthoritativeRemainderWithoutVanillaConsumption()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            ItemObject resultingSlotItem = RegisterItem(objectManager, out string resultingSlotItemId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                weapon.Amount = 5;
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    observer.Resolve<IBattleNetwork>(),
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        agentId,
                        worldItemId,
                        itemObjectId,
                        weapon,
                        isCatchUp: true));
                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        5,
                        3,
                        2,
                        false,
                        resultingSlotItemId,
                        null,
                        null,
                        3));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Equal(0, AgentEquipmentShim.WorldItemPickupCount);
                Assert.Same(resultingSlotItem, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(3, equipment[EquipmentIndex.Weapon0].Amount);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity remainingItem));
                Assert.Equal(2, remainingItem.WeaponCopy.Amount);
            }
            finally
            {
                objectManager.Remove(itemObject);
                objectManager.Remove(resultingSlotItem);
            }
        });
    }

    [Fact]
    public void CatchUpBeforePickup_ConsumesRegisteredItemAndRetiresReplay()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var dropHandler = new WeaponDropHandler(
                    agentRegistry,
                    worldItemRegistry,
                    messageBroker,
                    network,
                    objectManager,
                    spawner);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    network,
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        agentId,
                        worldItemId,
                        itemObjectId,
                        weapon,
                        isCatchUp: true));
                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        1,
                        1,
                        0,
                        true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Equal(1, spawner.SpawnCount);
                Assert.Equal(1, AgentEquipmentShim.WorldItemPickupCount);
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
                network.NetworkSentMessages.Clear();
                dropHandler.CatchUpJoiner("later-joiner");
                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    private void RunWithAgentShims(Action<EnvironmentInstance> test)
    {
        var harmony = new Harmony("e2e.weapon-drop-convergence." + Guid.NewGuid().ToString("N"));
        PatchAgentEquipment(harmony);

        try
        {
            EnvironmentInstance observer = Clients.First();
            SetControllerId(observer, "observer");
            observer.Call(() => test(observer));
        }
        finally
        {
            AgentEquipmentShim.Clear();
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static WeaponDropHandler CreateHandler(
        EnvironmentInstance observer,
        INetworkAgentRegistry agentRegistry,
        INetworkWorldItemRegistry worldItemRegistry,
        IObjectManager objectManager,
        IWeaponDropWorldItemSpawner spawner,
        IMessageBroker messageBroker) =>
        new WeaponDropHandler(
            agentRegistry,
            worldItemRegistry,
            messageBroker,
            observer.Resolve<IBattleNetwork>(),
            objectManager,
            spawner);

    private static NetworkWeaponDropped CreateDropMessage(
        Guid agentId,
        Guid worldItemId,
        string itemObjectId,
        MissionWeapon weapon,
        EquipmentIndex equipmentIndex = EquipmentIndex.Weapon0,
        bool isCatchUp = false,
        AgentEquipmentData? currentEquipment = null) =>
        new NetworkWeaponDropped(
            worldItemId,
            agentId,
            equipmentIndex,
            worldItemId,
            "fighter",
            itemObjectId,
            null,
            null,
            weapon.RawDataForNetwork,
            Vec3.Zero,
            Mat3.Identity,
            (int)Mission.WeaponSpawnFlags.WithPhysics,
            hasLifeTime: true,
            remainingLifeTime: 180f,
            currentEquipment,
            isCatchUp);

    private static ItemObject RegisterItem(
        IObjectManager objectManager,
        out string itemObjectId)
    {
        ItemObject item = ObjectHelper.SkipConstructor<ItemObject>();
        item.AddWeapon(
            new WeaponComponentData(null, WeaponClass.OneHandedSword, default),
            null);
        itemObjectId = "weapon_drop_test_" + Guid.NewGuid().ToString("N");
        Assert.True(objectManager.AddExisting(itemObjectId, item));
        return item;
    }

    private static MissionWeapon CreateWeapon(ItemObject item) =>
        item == null ? default : new MissionWeapon(item, null, null);

    private static MissionEquipment CreateEquipment(MissionWeapon weapon)
    {
        var equipment = new MissionEquipment();
        var weapons = new MissionWeapon[(int)EquipmentIndex.NumAllWeaponSlots];
        weapons[(int)EquipmentIndex.Weapon0] = weapon;
        typeof(MissionEquipment)
            .GetField("_weaponSlots", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(equipment, weapons);
        return equipment;
    }

    private static void InvokeReconcileRuntimeEquipment(
        CoopTournamentController controller,
        Agent agent)
    {
        MethodInfo reconcile = typeof(CoopTournamentController).GetMethod(
            "ReconcileRuntimeEquipment",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(reconcile);
        reconcile.Invoke(
            controller,
            new object[] { agent, Array.Empty<TournamentMissionWeaponData>() });
    }

    private static void ExpirePendingObservedDrop(
        WeaponDropHandler handler,
        Guid agentId,
        EquipmentIndex equipmentIndex)
    {
        FieldInfo pendingField = typeof(WeaponDropHandler).GetField(
            "pendingDrops",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(pendingField);
        object pending = pendingField.GetValue(handler);
        object key = (agentId, equipmentIndex);
        object queue = pending.GetType().GetProperty("Item").GetValue(pending, new[] { key });
        object observed = queue.GetType().GetMethod("Peek").Invoke(queue, null);
        FieldInfo expiresField = observed.GetType().GetField(
            "<ExpiresAtUtc>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(expiresField);
        expiresField.SetValue(observed, DateTime.MinValue);

        MethodInfo expiry = typeof(WeaponDropHandler).GetMethod(
            "CheckObservedDropExpiry",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(expiry);
        expiry.Invoke(handler, new[] { key, observed });
    }

    private static void PatchAgentEquipment(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
            prefix: Prefix(nameof(AgentEquipmentShim.SkipScriptComponentCache)));
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.Current)),
            prefix: Prefix(nameof(AgentEquipmentShim.GetCurrentMission)));
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.Equipment)),
            prefix: Prefix(nameof(AgentEquipmentShim.GetEquipment)));
        harmony.Patch(
            AccessTools.Method(typeof(Agent), nameof(Agent.RemoveEquippedWeapon)),
            prefix: Prefix(nameof(AgentEquipmentShim.RemoveEquippedWeapon)));
        harmony.Patch(
            AccessTools.Method(
                typeof(Agent),
                nameof(Agent.EquipWeaponWithNewEntity),
                new[] { typeof(EquipmentIndex), typeof(MissionWeapon).MakeByRefType() }),
            prefix: Prefix(nameof(AgentEquipmentShim.EquipWeaponWithNewEntity)));
        harmony.Patch(
            AccessTools.Method(typeof(Agent), nameof(Agent.IsActive)),
            prefix: Prefix(nameof(AgentEquipmentShim.IsActive)));
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsHuman)),
            prefix: Prefix(nameof(AgentEquipmentShim.GetIsHuman)));
        harmony.Patch(
            AccessTools.Method(typeof(Agent), nameof(Agent.SetWeaponAmountInSlot)),
            prefix: Prefix(nameof(AgentEquipmentShim.SetWeaponAmountInSlot)));
        harmony.Patch(
            AccessTools.Method(typeof(Agent), nameof(Agent.GetWeaponEntityFromEquipmentSlot)),
            prefix: Prefix(nameof(AgentEquipmentShim.GetWeaponEntity)));
        harmony.Patch(
            AccessTools.Method(typeof(WeaponPickupHandler), "ApplyWorldItemPickup"),
            prefix: Prefix(nameof(AgentEquipmentShim.SkipWorldItemPickup)));
        harmony.Patch(
            AccessTools.Method(typeof(WeaponPickupHandler), nameof(WeaponPickupHandler.IsWorldItemAvailable)),
            prefix: Prefix(nameof(AgentEquipmentShim.IsWorldItemAvailable)));
        harmony.Patch(
            AccessTools.Method(
                typeof(Agent),
                nameof(Agent.DropItem),
                new[] { typeof(EquipmentIndex), typeof(WeaponClass) }),
            prefix: Prefix(nameof(AgentEquipmentShim.DropItem)));
    }

    private static HarmonyMethod Prefix(string methodName) =>
        new(AccessTools.Method(typeof(AgentEquipmentShim), methodName));

    private sealed class RecordingWorldItemSpawner : IWeaponDropWorldItemSpawner
    {
        private readonly HashSet<SpawnedItemEntity> presentItems = new();

        public int SpawnCount { get; private set; }
        public SpawnedItemEntity LastSpawnedItem { get; private set; }

        public SpawnedItemEntity AddPresent(MissionWeapon weapon)
        {
            SpawnedItemEntity item = ObjectHelper.SkipConstructor<SpawnedItemEntity>();
            item._weapon = weapon;
            presentItems.Add(item);
            return item;
        }

        public bool IsPresent(SpawnedItemEntity item) =>
            item != null && presentItems.Contains(item);

        public bool TryGetState(
            SpawnedItemEntity item,
            out MatrixFrame frame,
            out float remainingLifeTime)
        {
            frame = MatrixFrame.Identity;
            remainingLifeTime = 180f;
            return IsPresent(item);
        }

        public bool TrySpawn(
            ref MissionWeapon weapon,
            Mission.WeaponSpawnFlags spawnFlags,
            bool hasLifeTime,
            float remainingLifeTime,
            MatrixFrame frame,
            out SpawnedItemEntity item)
        {
            SpawnCount++;
            item = AddPresent(weapon);
            item._hasLifeTime = hasLifeTime;
            item.SpawnFlags = spawnFlags;
            LastSpawnedItem = item;
            return true;
        }

        public bool TryRemove(SpawnedItemEntity item) =>
            item == null || presentItems.Remove(item);
    }

    private static class AgentEquipmentShim
    {
        private sealed class State
        {
            public MissionEquipment Equipment { get; }
            public int DropCount { get; set; }
            public int RemoveCount { get; set; }
            public int EquipCount { get; set; }
            public int WeaponEntityLookupCount { get; set; }

            public State(MissionEquipment equipment)
            {
                Equipment = equipment;
            }
        }

        private static readonly Dictionary<Agent, State> States = new();

        public static int WorldItemPickupCount { get; private set; }

        public static void Track(Agent agent, MissionEquipment equipment) =>
            States.Add(agent, new State(equipment));

        public static int GetDropCount(Agent agent) => States[agent].DropCount;
        public static int GetRemoveCount(Agent agent) => States[agent].RemoveCount;
        public static int GetEquipCount(Agent agent) => States[agent].EquipCount;

        public static int GetWeaponEntityLookupCount(Agent agent) =>
            States[agent].WeaponEntityLookupCount;

        public static void Clear()
        {
            States.Clear();
            WorldItemPickupCount = 0;
        }

        public static bool SkipScriptComponentCache() => false;

        public static bool GetCurrentMission(ref Mission __result)
        {
            __result = null;
            return false;
        }

        public static bool GetEquipment(Agent __instance, ref MissionEquipment __result)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            __result = state.Equipment;
            return false;
        }

        public static bool RemoveEquippedWeapon(Agent __instance, EquipmentIndex slotIndex)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            state.RemoveCount++;
            state.Equipment[slotIndex] = default;
            return false;
        }

        public static bool EquipWeaponWithNewEntity(
            Agent __instance,
            EquipmentIndex slotIndex,
            ref MissionWeapon weapon)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            state.EquipCount++;
            state.Equipment[slotIndex] = weapon;
            return false;
        }

        public static bool IsActive(Agent __instance, ref bool __result)
        {
            if (!States.ContainsKey(__instance)) return true;
            __result = true;
            return false;
        }

        public static bool GetIsHuman(Agent __instance, ref bool __result)
        {
            if (!States.ContainsKey(__instance)) return true;
            __result = false;
            return false;
        }

        public static bool SetWeaponAmountInSlot(
            Agent __instance,
            EquipmentIndex __0,
            short __1)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            MissionWeapon weapon = state.Equipment[__0];
            weapon.Amount = __1;
            state.Equipment[__0] = weapon;
            return false;
        }

        public static bool GetWeaponEntity(
            Agent __instance,
            EquipmentIndex slotIndex,
            ref WeakGameEntity __result)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            state.WeaponEntityLookupCount++;
            if (!state.Equipment[slotIndex].IsEmpty) return true;
            __result = default;
            return false;
        }

        public static bool SkipWorldItemPickup()
        {
            WorldItemPickupCount++;
            return false;
        }

        public static bool IsWorldItemAvailable(ref bool __result)
        {
            __result = true;
            return false;
        }

        public static bool DropItem(Agent __instance, EquipmentIndex itemIndex)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            state.DropCount++;
            state.Equipment[itemIndex] = default;
            return false;
        }
    }
}
