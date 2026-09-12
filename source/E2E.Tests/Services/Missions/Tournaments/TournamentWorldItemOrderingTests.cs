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
    public void ObservedDrop_PendingIdentityClearsWhenCanonicalDropArrives()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(agent, CreateEquipment(weapon));

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));
                var worldItemRegistry = new NetworkWorldItemRegistry();
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
                        EquipmentIndex.Weapon0,
                        weapon,
                        observedItem));
                Assert.True(handler.IsWorldItemIdentityPending(observedItem));

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.False(handler.IsWorldItemIdentityPending(observedItem));
                Assert.True(worldItemRegistry.TryGet(worldItemId, out var canonicalItem));
                Assert.Same(observedItem, canonicalItem);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void TournamentRuntimeReplay_PendingObservedItemDoesNotAllocateIdentity()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(agent, CreateEquipment(weapon));
                var controller = observer.Resolve<CoopTournamentController>();
                WeaponDropHandler dropHandler = GetWeaponDropHandler(controller);
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                Guid agentId = Guid.NewGuid();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));
                SpawnedItemEntity observedItem = new RecordingWorldItemSpawner().AddPresent(weapon);
                observer.Resolve<IMessageBroker>().Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        weapon,
                        observedItem));

                Assert.True(dropHandler.IsWorldItemIdentityPending(observedItem));
                Assert.False(controller.TryGetRuntimeWorldItemId(observedItem, out _));
                Assert.False(observer.Resolve<INetworkWorldItemRegistry>().TryGetId(observedItem, out _));

                ExpirePendingObservedDrop(dropHandler, agentId, EquipmentIndex.Weapon0);
                Assert.False(dropHandler.IsWorldItemIdentityPending(observedItem));
                Assert.True(controller.TryAllocateRuntimeWorldItemId(
                    observedItem,
                    isAvailable: true,
                    out Guid worldItemId));
                Assert.NotEqual(Guid.Empty, worldItemId);
                Assert.True(observer.Resolve<INetworkWorldItemRegistry>().TryGetId(observedItem, out _));
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void IncomingDrop_LocalHostForwardsCanonicalDropOnce()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(agent, CreateEquipment(weapon));

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var handler = CreateHandler(
                    observer,
                    agentRegistry,
                    new NetworkWorldItemRegistry(),
                    objectManager,
                    spawner,
                    messageBroker);
                handler.ConfigureLocalHostProvider(() => true);
                NetworkWeaponDropped drop = CreateDropMessage(
                    agentId,
                    worldItemId,
                    itemObjectId,
                    weapon);

                messageBroker.Publish(this, drop);
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                NetworkWeaponDropped forwarded = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
                Assert.Equal(drop.DropId, forwarded.DropId);
                Assert.False(forwarded.IsCatchUp);

                messageBroker.Publish(this, drop);
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.Single(network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

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
                        resultingWeapon,
                        pickupId: Guid.NewGuid()));

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

                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        pickerId,
                        worldItemId,
                        itemObjectId,
                        staleWeapon));

                Assert.Same(itemObject, picker.Equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(3, picker.Equipment[EquipmentIndex.Weapon0].Amount);
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
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
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
    public void UncorrelatedRuntimePickup_IsRejectedWithoutDetachedBroadcast()
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

                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.True(picker.Equipment[EquipmentIndex.Weapon0].IsEmpty);
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
    public void AuthoritativeDropAfterPickupExpiry_IsNotClaimedByOldObservation()
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
                observedItem.Id = new MissionObjectId(79, true);
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

                ExpirePendingObservedDrop(dropHandler, dropperId, EquipmentIndex.Weapon0);
                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.True(picker.Equipment[EquipmentIndex.Weapon0].IsEmpty);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(dropperId, worldItemId, itemObjectId, weapon));

                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.True(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.Equal(1, spawner.SpawnCount);

                network.NetworkSentMessages.Clear();
                dropHandler.CatchUpJoiner("later-joiner");
                NetworkWeaponDropped catchUp = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
                Assert.Equal(worldItemId, catchUp.WorldItemId);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ConsumedPickupBeforeLiveDrop_ClearsSourceSlotWithoutRespawningItem()
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
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
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
                    network,
                    messageBroker,
                    objectManager);

                SpawnedItemEntity observedItem = spawner.AddPresent(weapon);
                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        weapon,
                        observedItem));
                Assert.True(spawner.IsPresent(observedItem));

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        default,
                        0,
                        1,
                        1,
                        0,
                        true,
                        resultingSlotItemObjectId: itemObjectId,
                        isIdentityCorrection: true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.False(equipment[EquipmentIndex.Weapon0].IsEmpty);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.False(spawner.IsPresent(observedItem));
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.Equal(0, spawner.SpawnCount);
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

    [Fact]
    public void MismatchedAuthoritativeDrop_RejectsDeferredPickupWithoutCanonicalIdentity()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject observedItemObject = RegisterItem(objectManager, out _);
            ItemObject canonicalItemObject = RegisterItem(objectManager, out string canonicalItemId);
            try
            {
                Agent dropper = ObjectHelper.SkipConstructor<Agent>();
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon observedWeapon = CreateWeapon(observedItemObject);
                MissionWeapon canonicalWeapon = CreateWeapon(canonicalItemObject);
                AgentEquipmentShim.Track(dropper, CreateEquipment(default));
                AgentEquipmentShim.Track(picker, CreateEquipment(observedWeapon));

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
                SpawnedItemEntity observedItem = spawner.AddPresent(observedWeapon);
                observedItem.Id = new MissionObjectId(80, true);
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
                        observedWeapon,
                        observedItem));
                spawner.TryRemove(observedItem);
                messageBroker.Publish(
                    this,
                    new WeaponPickedup(
                        picker,
                        observedItem,
                        EquipmentIndex.Weapon0,
                        observedItemObject,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        1,
                        1,
                        0,
                        true,
                        observedWeapon));

                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        dropperId,
                        worldItemId,
                        canonicalItemId,
                        canonicalWeapon));

                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.True(picker.Equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out _));
            }
            finally
            {
                objectManager.Remove(observedItemObject);
                objectManager.Remove(canonicalItemObject);
            }
        });
    }

    [Fact]
    public void EmptyAuthoritativeWorldItemId_RejectsDeferredPickup()
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
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                Assert.True(agentRegistry.TryRegisterAgent("fighter", dropperId, dropper));
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity observedItem = spawner.AddPresent(weapon);
                observedItem.Id = new MissionObjectId(81, true);
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

                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        dropperId,
                        Guid.Empty,
                        itemObjectId,
                        weapon,
                        dropId: Guid.NewGuid()));

                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.True(picker.Equipment[EquipmentIndex.Weapon0].IsEmpty);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void DropAfterSourceAgentRemoved_RecordsSurvivingWorldItemForCatchUp()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(agent, CreateEquipment(default));

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
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
                        EquipmentIndex.Weapon0,
                        weapon,
                        observedItem));
                Assert.True(agentRegistry.RemoveAgent(agentId));
                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));

                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registeredItem));
                Assert.Same(observedItem, registeredItem);
                handler.CatchUpJoiner("joiner");
                NetworkWeaponDropped catchUp = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
                Assert.True(catchUp.IsCatchUp);
                Assert.Equal(worldItemId, catchUp.WorldItemId);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ResolvedDeferredPickup_AfterPickerRemoved_PreservesPickerId()
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
                observedItem.Id = new MissionObjectId(82, true);
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
                Assert.True(agentRegistry.RemoveAgent(pickerId));

                messageBroker.Publish(
                    this,
                    CreateDropMessage(dropperId, worldItemId, itemObjectId, weapon));

                NetworkWeaponPickedup sent = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.Equal(pickerId, sent.AgentId);
                Assert.Equal(worldItemId, sent.WorldItemId);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PickupAfterAgentRemoved_RetiresWorldItemWithoutSlotApply()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(agent, CreateEquipment(default));

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
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
                    network,
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity worldItem));
                Assert.True(spawner.IsPresent(worldItem));
                Assert.True(agentRegistry.RemoveAgent(agentId));

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        default,
                        0,
                        1,
                        1,
                        0,
                        true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.False(spawner.IsPresent(worldItem));
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

    [Fact]
    public void PickupBeforeAgentRegistration_AppliesSlotWhenAgentBecomesActive()
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
                    network,
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity worldItem));

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        default,
                        0,
                        1,
                        1,
                        0,
                        true,
                        pickupId: Guid.NewGuid()));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.False(spawner.IsPresent(worldItem));
                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);

                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));
                pickupHandler.Tick(1f);

                Assert.Same(itemObject, equipment[EquipmentIndex.Weapon0].Item);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ObservedDropExpiry_DoesNotQueueAnActionBeforeDeadline()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(agent, CreateEquipment(default));

                Guid agentId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
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

                Common.GameThread.Instance.Update(TimeSpan.Zero);
                int queueLength = Common.GameThread.Instance.QueueLength;
                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        weapon,
                        observedItem));

                Assert.Equal(queueLength, Common.GameThread.Instance.QueueLength);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ObservedDropExpiry_DoesNotRollbackAfterLocalAuthorityTransfer()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject observedItemObject = RegisterItem(objectManager, out _);
            ItemObject localItemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon observedWeapon = CreateWeapon(observedItemObject);
                MissionWeapon localWeapon = CreateWeapon(localItemObject);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
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

                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        agent,
                        EquipmentIndex.Weapon0,
                        observedWeapon,
                        observedItem));

                Assert.True(agentRegistry.TryTransferAuthority("observer", agentId));
                equipment[EquipmentIndex.Weapon0] = localWeapon;
                ExpirePendingObservedDrop(handler, agentId, EquipmentIndex.Weapon0);

                Assert.Same(localItemObject, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(0, AgentEquipmentShim.GetRemoveCount(agent));
                Assert.Equal(0, AgentEquipmentShim.GetEquipCount(agent));
                Assert.False(spawner.IsPresent(observedItem));
            }
            finally
            {
                objectManager.Remove(observedItemObject);
                objectManager.Remove(localItemObject);
            }
        });
    }

    [Fact]
    public void PendingPickupObservations_StayWithinPerSlotLimitWithoutDetachedFallback()
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
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                Assert.True(agentRegistry.TryRegisterAgent("fighter", dropperId, dropper));
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

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
                    network,
                    messageBroker,
                    objectManager);

                for (int index = 0; index < 9; index++)
                {
                    SpawnedItemEntity observedItem = spawner.AddPresent(weapon);
                    observedItem.Id = new MissionObjectId(100 + index, true);
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
                }

                Assert.Equal(
                    8,
                    GetPendingObservedDropCount(
                        dropHandler,
                        dropperId,
                        EquipmentIndex.Weapon0));
                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
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
    public void ObservedDropTimeout_AfterIdentitylessPickup_RestoresObservedWeapon()
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
                Assert.Same(observedItemObject, equipment[EquipmentIndex.Weapon0].Item);
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
    public void ObservedPartialPickupTimeout_RetainsRemainderUntilAuthoritativeDrop()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent dropper = ObjectHelper.SkipConstructor<Agent>();
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon droppedWeapon = CreateWeapon(itemObject);
                droppedWeapon.Amount = 5;
                MissionWeapon resultingWeapon = CreateWeapon(itemObject);
                resultingWeapon.Amount = 3;
                AgentEquipmentShim.Track(dropper, CreateEquipment(default));
                AgentEquipmentShim.Track(picker, CreateEquipment(resultingWeapon));

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
                SpawnedItemEntity observedItem = spawner.AddPresent(droppedWeapon);
                observedItem.Id = new MissionObjectId(82, true);
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
                        droppedWeapon,
                        observedItem));
                observedItem._weapon.Amount = 2;
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
                        5,
                        3,
                        2,
                        false,
                        resultingWeapon,
                        pickupId: Guid.NewGuid()));

                ExpirePendingObservedDrop(dropHandler, dropperId, EquipmentIndex.Weapon0);

                Assert.True(spawner.IsPresent(observedItem));
                Assert.Equal(2, observedItem.WeaponCopy.Amount);
                Assert.Equal(1, GetPendingObservedDropCount(
                    dropHandler,
                    dropperId,
                    EquipmentIndex.Weapon0));
                Assert.Equal(0, AgentEquipmentShim.GetEquipCount(dropper));

                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        dropperId,
                        worldItemId,
                        itemObjectId,
                        droppedWeapon));

                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registered));
                Assert.Same(observedItem, registered);
                Assert.Equal(2, registered.WeaponCopy.Amount);
                Assert.Equal(0, spawner.SpawnCount);
                Assert.Equal(0, GetPendingObservedDropCount(
                    dropHandler,
                    dropperId,
                    EquipmentIndex.Weapon0));
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ConsumedPickupAfterObservedPartialTimeout_RechecksExpiry()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent dropper = ObjectHelper.SkipConstructor<Agent>();
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon droppedWeapon = CreateWeapon(itemObject);
                droppedWeapon.Amount = 5;
                MissionWeapon partialResult = CreateWeapon(itemObject);
                partialResult.Amount = 3;
                MissionWeapon consumedResult = CreateWeapon(itemObject);
                consumedResult.Amount = 5;
                AgentEquipmentShim.Track(dropper, CreateEquipment(default));
                AgentEquipmentShim.Track(picker, CreateEquipment(consumedResult));

                Guid dropperId = Guid.NewGuid();
                Guid pickerId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                Assert.True(agentRegistry.TryRegisterAgent("fighter", dropperId, dropper));
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity observedItem = spawner.AddPresent(droppedWeapon);
                observedItem.Id = new MissionObjectId(83, true);
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
                        droppedWeapon,
                        observedItem));
                observedItem._weapon.Amount = 2;
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
                        5,
                        3,
                        2,
                        false,
                        partialResult,
                        pickupId: Guid.NewGuid()));
                ExpirePendingObservedDrop(dropHandler, dropperId, EquipmentIndex.Weapon0);

                Assert.Equal(1, GetPendingObservedDropCount(
                    dropHandler,
                    dropperId,
                    EquipmentIndex.Weapon0));

                Assert.True(spawner.TryRemove(observedItem));
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
                        3,
                        2,
                        5,
                        0,
                        true,
                        consumedResult,
                        previousSlotWeapon: partialResult,
                        pickupId: Guid.NewGuid()));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Equal(0, GetPendingObservedDropCount(
                    dropHandler,
                    dropperId,
                    EquipmentIndex.Weapon0));
                Assert.Same(itemObject, dropper.Equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(1, AgentEquipmentShim.GetEquipCount(dropper));
                Assert.True(picker.Equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
            }
            finally
            {
                objectManager.Remove(itemObject);
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
    public void MismatchedRegisteredItem_RemainsRegisteredWhenRemovalFails()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject canonicalItem = RegisterItem(objectManager, out string canonicalItemId);
            ItemObject mismatchedItem = RegisterItem(objectManager, out _);
            try
            {
                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity mismatchedWorldItem =
                    spawner.AddPresent(CreateWeapon(mismatchedItem));
                worldItemRegistry.Register(worldItemId, mismatchedWorldItem);
                spawner.FailRemovals = true;

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
                    canonicalItemId,
                    CreateWeapon(canonicalItem));

                messageBroker.Publish(this, message);

                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registered));
                Assert.Same(mismatchedWorldItem, registered);
                Assert.True(spawner.IsPresent(mismatchedWorldItem));
                Assert.Equal(0, spawner.SpawnCount);
                Assert.Equal(1, spawner.PresentCount);

                spawner.FailRemovals = false;
                messageBroker.Publish(this, message);

                Assert.False(spawner.IsPresent(mismatchedWorldItem));
                Assert.True(worldItemRegistry.TryGet(worldItemId, out registered));
                Assert.Same(canonicalItem, registered.WeaponCopy.Item);
                Assert.Equal(1, spawner.SpawnCount);
                Assert.Equal(1, spawner.PresentCount);
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
    public void PickupWithMismatchedRegistration_WaitsForDropRepair()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject expectedItem = RegisterItem(objectManager, out string expectedItemId);
            ItemObject wrongItem = RegisterItem(objectManager, out _);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon expectedWeapon = CreateWeapon(expectedItem);
                MissionWeapon wrongWeapon = CreateWeapon(wrongItem);
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity wrongWorldItem = spawner.AddPresent(wrongWeapon);
                worldItemRegistry.Register(worldItemId, wrongWorldItem);
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
                        expectedItemId,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        1,
                        1,
                        0,
                        true,
                        worldItemDataValue: expectedWeapon.RawDataForNetwork,
                        hasWorldItemDataValue: true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registered));
                Assert.Same(wrongWorldItem, registered);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, expectedItemId, expectedWeapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(expectedItem, equipment[EquipmentIndex.Weapon0].Item);
                Assert.False(spawner.IsPresent(wrongWorldItem));
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.Equal(1, spawner.SpawnCount);
            }
            finally
            {
                objectManager.Remove(expectedItem);
                objectManager.Remove(wrongItem);
            }
        });
    }

    [Fact]
    public void PartialPickupWithMismatchedWeaponData_WaitsForDropRepair()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon expectedWeapon = CreateWeapon(itemObject);
                expectedWeapon.Amount = 5;
                MissionWeapon wrongWeapon = new MissionWeapon(
                    itemObject,
                    null,
                    null,
                    (short)(expectedWeapon.RawDataForNetwork + 1));
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity wrongWorldItem = spawner.AddPresent(wrongWeapon);
                worldItemRegistry.Register(worldItemId, wrongWorldItem);
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
                        false,
                        worldItemDataValue: expectedWeapon.RawDataForNetwork,
                        hasWorldItemDataValue: true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.True(spawner.IsPresent(wrongWorldItem));

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, expectedWeapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(itemObject, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(3, equipment[EquipmentIndex.Weapon0].Amount);
                Assert.False(spawner.IsPresent(wrongWorldItem));
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity remaining));
                Assert.Equal(2, remaining.WeaponCopy.Amount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PickupBeforeCatchUp_AppliesCanonicalConsumedResultWithoutRespawn()
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

    [Fact]
    public void CatchUpAfterManyConsumedWorldItems_DoesNotRespawnAppliedDrop()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            ItemObject laterItem = RegisterItem(objectManager, out _);
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

                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                dropHandler.ConfigureLocalHostProvider(() => true);
                NetworkWeaponDropped drop = CreateDropMessage(
                    agentId,
                    worldItemId,
                    itemObjectId,
                    weapon);

                messageBroker.Publish(this, drop);
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.Equal(1, spawner.SpawnCount);

                Guid consumedPickupId = Guid.NewGuid();
                messageBroker.Publish(
                    this,
                    new WeaponPickupApplied(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        resultingWorldItemAmount: 0,
                        worldItemConsumed: true,
                        pickupId: consumedPickupId));
                const int rolloverCount = 513;
                for (int i = 0; i < rolloverCount; i++)
                {
                    Guid rolloverWorldItemId = Guid.NewGuid();
                    messageBroker.Publish(
                        this,
                        CreateDropMessage(
                            agentId,
                            rolloverWorldItemId,
                            itemObjectId,
                            weapon));
                    messageBroker.Publish(
                        this,
                        new WeaponPickupApplied(
                            agentId,
                            EquipmentIndex.Weapon0,
                            rolloverWorldItemId,
                            resultingWorldItemAmount: 0,
                            worldItemConsumed: true,
                            pickupId: Guid.NewGuid()));
                }

                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.Equal(rolloverCount + 1, spawner.SpawnCount);
                MissionWeapon laterWeapon = CreateWeapon(laterItem);
                equipment[EquipmentIndex.Weapon0] = laterWeapon;
                int removeCount = AgentEquipmentShim.GetRemoveCount(agent);

                messageBroker.Publish(this, drop);
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Equal(rolloverCount + 1, spawner.SpawnCount);
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.Same(laterItem, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(removeCount, AgentEquipmentShim.GetRemoveCount(agent));

                network.NetworkSentMessages.Clear();
                Guid requestId = Guid.NewGuid();
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        requestId,
                        new[] { consumedPickupId }));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                NetworkWeaponDropStateResponse response = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.Equal(requestId, response.RequestId);
                Assert.True(response.WorldItemConsumed);
                Assert.Null(response.Drop);
            }
            finally
            {
                objectManager.Remove(itemObject);
                objectManager.Remove(laterItem);
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

                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                dropHandler.ConfigureLocalHostProvider(() => true);

                messageBroker.Publish(
                    this,
                    new WeaponPickupApplied(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        resultingWorldItemAmount: 2,
                        worldItemConsumed: false,
                        pickupId: Guid.NewGuid()));
                ExpireWorldItemTransitionState(dropHandler, worldItemId);
                dropHandler.Tick(1f);
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

                Assert.True(spawner.TryRemove(remainingItem));
                network.NetworkSentMessages.Clear();
                Guid requestId = Guid.NewGuid();
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        requestId));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                NetworkWeaponDropStateResponse response = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.Equal(requestId, response.RequestId);
                Assert.False(response.WorldItemConsumed);
                Assert.Equal(remainingItem.WeaponCopy.RawDataForNetwork, response.Drop.DataValue);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ConsumedCatchUpOnlyDrop_PreservesTerminalStateAfterPreDropExpiry()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon weapon = CreateWeapon(itemObject);
                AgentEquipmentShim.Track(agent, CreateEquipment(default));

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                Guid pickupId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker);
                dropHandler.ConfigureLocalHostProvider(() => true);

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
                    new WeaponPickupApplied(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        resultingWorldItemAmount: 0,
                        worldItemConsumed: true,
                        pickupId: pickupId));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                ExpireWorldItemTransitionState(dropHandler, worldItemId);
                dropHandler.Tick(1f);
                network.NetworkSentMessages.Clear();
                Guid requestId = Guid.NewGuid();
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        requestId,
                        new[] { pickupId }));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                NetworkWeaponDropStateResponse response = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.Equal(requestId, response.RequestId);
                Assert.True(response.WorldItemConsumed);
                Assert.Null(response.Drop);
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

    [Fact]
    public void CatchUpBeforeLiveDrop_DoesNotSuppressSlotTransition()
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
                var worldItemRegistry = new NetworkWorldItemRegistry();
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
                        itemObjectId,
                        weapon,
                        isCatchUp: true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(itemObject, equipment[EquipmentIndex.Weapon0].Item);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out _));

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.Equal(1, spawner.SpawnCount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ConsumedPickupBeforeDrop_PreservesNewerSlotState()
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
                        true,
                        worldItemDataValue: weapon.RawDataForNetwork,
                        hasWorldItemDataValue: true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(itemObject, equipment[EquipmentIndex.Weapon0].Item);
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));

                messageBroker.Publish(
                    this,
                    CreateDropMessage(agentId, worldItemId, itemObjectId, weapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(itemObject, equipment[EquipmentIndex.Weapon0].Item);
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
    public void ConsumedPickupBeforeDifferentAgentDrop_ClearsDropperOnly()
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
                MissionEquipment dropperEquipment = CreateEquipment(weapon);
                MissionEquipment pickerEquipment = CreateEquipment(default);
                AgentEquipmentShim.Track(dropper, dropperEquipment);
                AgentEquipmentShim.Track(picker, pickerEquipment);

                Guid dropperId = Guid.NewGuid();
                Guid pickerId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", dropperId, dropper));
                Assert.True(agentRegistry.TryRegisterAgent("picker", pickerId, picker));

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
                        pickerId,
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
                        worldItemDataValue: weapon.RawDataForNetwork,
                        hasWorldItemDataValue: true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(itemObject, pickerEquipment[EquipmentIndex.Weapon0].Item);
                Assert.Same(itemObject, dropperEquipment[EquipmentIndex.Weapon0].Item);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(dropperId, worldItemId, itemObjectId, weapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(itemObject, pickerEquipment[EquipmentIndex.Weapon0].Item);
                Assert.True(dropperEquipment[EquipmentIndex.Weapon0].IsEmpty);
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
    public void ReversedPartialPickups_DrainAfterRemovedPredecessorAdvancesState()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent firstPicker = ObjectHelper.SkipConstructor<Agent>();
                Agent secondPicker = ObjectHelper.SkipConstructor<Agent>();
                MissionEquipment firstEquipment = CreateEquipment(default);
                MissionEquipment secondEquipment = CreateEquipment(default);
                AgentEquipmentShim.Track(firstPicker, firstEquipment);
                AgentEquipmentShim.Track(secondPicker, secondEquipment);

                Guid firstPickerId = Guid.NewGuid();
                Guid secondPickerId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("first", firstPickerId, firstPicker));
                Assert.True(agentRegistry.TryRegisterAgent("second", secondPickerId, secondPicker));
                Assert.True(agentRegistry.RemoveAgent(firstPickerId));

                MissionWeapon amountTen = CreateWeapon(itemObject);
                amountTen.Amount = 10;
                MissionWeapon amountSeven = amountTen;
                amountSeven.Amount = 7;
                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity worldItem = spawner.AddPresent(amountTen);
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
                    observer.Resolve<IBattleNetwork>(),
                    messageBroker,
                    objectManager);

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        secondPickerId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        7,
                        3,
                        4,
                        false,
                        worldItemDataValue: amountSeven.RawDataForNetwork,
                        hasWorldItemDataValue: true));
                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickedup(
                        firstPickerId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        itemObjectId,
                        null,
                        null,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        0,
                        10,
                        3,
                        7,
                        false,
                        worldItemDataValue: amountTen.RawDataForNetwork,
                        hasWorldItemDataValue: true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(firstEquipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.Equal(3, secondEquipment[EquipmentIndex.Weapon0].Amount);
                Assert.Equal(4, worldItem.WeaponCopy.Amount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PartialPickupAfterResultingCatchUp_PreservesNewerSlotState()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon previousWorldWeapon = CreateWeapon(itemObject);
                previousWorldWeapon.Amount = 5;
                MissionWeapon resultingWorldWeapon = previousWorldWeapon;
                resultingWorldWeapon.Amount = 2;
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
                        resultingWorldWeapon,
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
                        worldItemDataValue: previousWorldWeapon.RawDataForNetwork,
                        hasWorldItemDataValue: true));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(itemObject, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(3, equipment[EquipmentIndex.Weapon0].Amount);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity remaining));
                Assert.Equal(2, remaining.WeaponCopy.Amount);

                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        agentId,
                        worldItemId,
                        itemObjectId,
                        previousWorldWeapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Same(itemObject, equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(3, equipment[EquipmentIndex.Weapon0].Amount);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out remaining));
                Assert.Equal(2, remaining.WeaponCopy.Amount);
                Assert.Equal(1, spawner.SpawnCount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ResolvedPendingIdentity_RecordsPickupLocallyBeforeBroadcast()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon resultingSlotWeapon = CreateWeapon(itemObject);
                resultingSlotWeapon.Amount = 3;
                AgentEquipmentShim.Track(picker, CreateEquipment(resultingSlotWeapon));
                Guid pickerId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                MissionWeapon worldWeapon = CreateWeapon(itemObject);
                worldWeapon.Amount = 7;
                SpawnedItemEntity worldItem =
                    new RecordingWorldItemSpawner().AddPresent(worldWeapon);
                worldItem.Id = new MissionObjectId(91, true);
                Guid worldItemId = Guid.NewGuid();

                var network = Assert.IsType<MockBattleNetwork>(
                    observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                network.NetworkSentMessages.Clear();
                using var messageBroker = new MessageBroker();
                WeaponPickupApplied? appliedPickup = null;
                messageBroker.Subscribe<WeaponPickupApplied>(
                    payload => appliedPickup = payload.What);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    new NetworkWorldItemRegistry(),
                    network,
                    messageBroker,
                    objectManager);

                messageBroker.Publish(this, new WorldItemIdentityPending(worldItem));
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
                        10,
                        3,
                        7,
                        false,
                        resultingSlotWeapon));

                Assert.Null(appliedPickup);
                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());

                messageBroker.Publish(
                    this,
                    new WorldItemIdentityResolved(worldItem, worldItemId));

                NetworkWeaponPickedup sentPickup = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.True(appliedPickup.HasValue);
                WeaponPickupApplied applied = appliedPickup.Value;
                Assert.NotEqual(Guid.Empty, applied.PickupId);
                Assert.Equal(sentPickup.PickupId, applied.PickupId);
                Assert.Equal(worldItemId, applied.WorldItemId);
                Assert.Equal((short)7, applied.ResultingWorldItemAmount);
                Assert.False(applied.WorldItemConsumed);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void AbandonedSuccessivePickups_RollBackInReverseOrder()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionEquipment equipment = CreateEquipment(default);
                AgentEquipmentShim.Track(picker, equipment);
                Guid pickerId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                MissionWeapon worldWeapon = CreateWeapon(itemObject);
                worldWeapon.Amount = 10;
                MissionWeapon firstResult = CreateWeapon(itemObject);
                firstResult.Amount = 3;
                MissionWeapon secondResult = firstResult;
                secondResult.Amount = 6;
                SpawnedItemEntity worldItem = new RecordingWorldItemSpawner().AddPresent(worldWeapon);
                worldItem.Id = new MissionObjectId(91, true);

                using var messageBroker = new MessageBroker();
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    new NetworkWorldItemRegistry(),
                    observer.Resolve<IBattleNetwork>(),
                    messageBroker,
                    objectManager);
                messageBroker.Publish(this, new WorldItemIdentityPending(worldItem));

                equipment[EquipmentIndex.Weapon0] = firstResult;
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
                        10,
                        3,
                        7,
                        false,
                        firstResult));

                equipment[EquipmentIndex.Weapon0] = secondResult;
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
                        3,
                        7,
                        6,
                        4,
                        false,
                        secondResult,
                        previousSlotWeapon: firstResult));

                messageBroker.Publish(this, new WorldItemIdentityAbandoned(worldItem));

                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PendingIdentityLimit_RestoresObservationBeforeAuthoritativeDrop()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Agent dropper = ObjectHelper.SkipConstructor<Agent>();
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon droppedWeapon = CreateWeapon(itemObject);
                droppedWeapon.Amount = 10;
                MissionEquipment dropperEquipment = CreateEquipment(default);
                MissionEquipment pickerEquipment = CreateEquipment(default);
                AgentEquipmentShim.Track(dropper, dropperEquipment);
                AgentEquipmentShim.Track(picker, pickerEquipment);

                Guid dropperId = Guid.NewGuid();
                Guid pickerId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", dropperId, dropper));
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity observedItem = spawner.AddPresent(droppedWeapon);
                observedItem.Id = new MissionObjectId(92, true);
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
                    new WeaponDropped(
                        dropper,
                        EquipmentIndex.Weapon0,
                        droppedWeapon,
                        observedItem));

                MissionWeapon previousSlotWeapon = default;
                for (short pickupNumber = 1; pickupNumber <= 9; pickupNumber++)
                {
                    MissionWeapon resultingSlotWeapon = CreateWeapon(itemObject);
                    resultingSlotWeapon.Amount = pickupNumber;
                    pickerEquipment[EquipmentIndex.Weapon0] = resultingSlotWeapon;
                    observedItem._weapon.Amount = (short)(10 - pickupNumber);
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
                            (short)(pickupNumber - 1),
                            (short)(11 - pickupNumber),
                            pickupNumber,
                            (short)(10 - pickupNumber),
                            false,
                            resultingSlotWeapon,
                            previousSlotWeapon));
                    previousSlotWeapon = resultingSlotWeapon;
                }

                Assert.True(pickerEquipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.Same(itemObject, dropperEquipment[EquipmentIndex.Weapon0].Item);
                Assert.False(spawner.IsPresent(observedItem));

                messageBroker.Publish(
                    this,
                    CreateDropMessage(
                        dropperId,
                        worldItemId,
                        itemObjectId,
                        droppedWeapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(dropperEquipment[EquipmentIndex.Weapon0].IsEmpty);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out _));
                Assert.Equal(1, spawner.SpawnCount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void PendingNetworkLimit_SlotRepairSupersedesDelayedPickups()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Guid worldItemId = Guid.NewGuid();
                Guid pickerId = Guid.NewGuid();
                Guid secondPickerId = Guid.NewGuid();
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                Agent secondPicker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon ownerSlotWeapon = CreateWeapon(itemObject);
                ownerSlotWeapon.Amount = 20;
                MissionWeapon secondOwnerSlotWeapon = CreateWeapon(itemObject);
                secondOwnerSlotWeapon.Amount = 30;
                AgentEquipmentShim.Track(picker, CreateEquipment(ownerSlotWeapon));
                AgentEquipmentShim.Track(secondPicker, CreateEquipment(secondOwnerSlotWeapon));
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));
                Assert.True(agentRegistry.TryRegisterAgent("observer", secondPickerId, secondPicker));
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                network.NetworkSentMessages.Clear();
                using var messageBroker = new MessageBroker();
                int localRequestCount = 0;
                Action<MessagePayload<NetworkWeaponDropResyncRequest>> countLocalRequest =
                    _ => localRequestCount++;
                messageBroker.Subscribe(countLocalRequest);
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    worldItemRegistry,
                    network,
                    messageBroker,
                    objectManager,
                    observer.Resolve<GameInterface.Services.Entity.IControllerIdProvider>());

                var pickupIds = new HashSet<Guid>();
                for (short pickupNumber = 1; pickupNumber <= 9; pickupNumber++)
                {
                    Guid pickupId = Guid.NewGuid();
                    pickupIds.Add(pickupId);
                    messageBroker.Publish(
                        this,
                        new NetworkWeaponPickedup(
                            pickerId,
                            EquipmentIndex.Weapon0,
                            worldItemId,
                            itemObjectId,
                            null,
                            null,
                            default,
                            0,
                            pickupNumber,
                            1,
                            (short)(pickupNumber - 1),
                            false,
                            pickupId: pickupId));
                }
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                NetworkWeaponDropResyncRequest request = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropResyncRequest>());
                Assert.Equal(worldItemId, request.WorldItemId);
                Assert.Equal("observer", request.RequesterControllerId);
                Assert.NotEqual(Guid.Empty, request.RequestId);
                Assert.Equal(new[] { pickerId }, request.AgentIds);
                Assert.Equal(new[] { EquipmentIndex.Weapon0 }, request.EquipmentIndices);
                Assert.True(pickupIds.SetEquals(request.RequiredPickupIds));
                Assert.Equal(1, localRequestCount);

                network.NetworkSentMessages.Clear();
                for (short pickupNumber = 10; pickupNumber <= 17; pickupNumber++)
                {
                    Guid pickupId = Guid.NewGuid();
                    pickupIds.Add(pickupId);
                    messageBroker.Publish(
                        this,
                        new NetworkWeaponPickedup(
                            secondPickerId,
                            EquipmentIndex.Weapon0,
                            worldItemId,
                            itemObjectId,
                            null,
                            null,
                            default,
                            0,
                            pickupNumber,
                            1,
                            (short)(pickupNumber - 1),
                            false,
                            pickupId: pickupId));
                }
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                NetworkWeaponDropResyncRequest mergedRequest = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropResyncRequest>());
                Assert.Equal(request.RequestId, mergedRequest.RequestId);
                Assert.True(pickupIds.SetEquals(mergedRequest.RequiredPickupIds));
                Assert.True(
                    new HashSet<Guid> { pickerId, secondPickerId }
                        .SetEquals(mergedRequest.AgentIds));
                request = mergedRequest;
                network.NetworkSentMessages.Clear();

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickupSlotState(
                        pickerId,
                        EquipmentIndex.Weapon0,
                        itemObjectId,
                        null,
                        null,
                        ownerSlotWeapon.RawDataForNetwork,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        request.RequestId,
                        worldItemId,
                        stateRevision: 0,
                        responderControllerId: "observer"));
                Assert.True(agentRegistry.RemoveAgent(secondPickerId));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                pickupHandler.Tick(4f);
                FieldInfo repeatedMergeRequestsField = typeof(WeaponPickupHandler).GetField(
                    "latestResyncRequests",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(repeatedMergeRequestsField);
                var repeatedMergeRequests = Assert.IsAssignableFrom<System.Collections.IDictionary>(
                    repeatedMergeRequestsField.GetValue(pickupHandler));
                object repeatedMergeRequest = repeatedMergeRequests[worldItemId];
                MethodInfo mergeRequest = repeatedMergeRequest.GetType().GetMethod(
                    "Merge",
                    BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo repeatedPendingTargets = repeatedMergeRequest.GetType().GetProperty(
                    "PendingTargets",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.NotNull(mergeRequest);
                Assert.NotNull(repeatedPendingTargets);
                var repeatedTarget = new HashSet<(Guid AgentId, EquipmentIndex Slot)>
                {
                    (secondPickerId, EquipmentIndex.Weapon0),
                };
                mergeRequest.Invoke(
                    repeatedMergeRequest,
                    new object[] { repeatedTarget, new HashSet<Guid>() });
                pickupHandler.Tick(1f);
                Assert.DoesNotContain(
                    (secondPickerId, EquipmentIndex.Weapon0),
                    Assert.IsAssignableFrom<
                        IEnumerable<(Guid AgentId, EquipmentIndex Slot)>>(
                        repeatedPendingTargets.GetValue(repeatedMergeRequest)));
                network.NetworkSentMessages.Clear();

                Assert.True(
                    agentRegistry.TryRegisterAgent(
                        "observer",
                        secondPickerId,
                        secondPicker));
                mergeRequest.Invoke(
                    repeatedMergeRequest,
                    new object[] { repeatedTarget, new HashSet<Guid>() });
                Assert.Contains(
                    (secondPickerId, EquipmentIndex.Weapon0),
                    Assert.IsAssignableFrom<
                        IEnumerable<(Guid AgentId, EquipmentIndex Slot)>>(
                        repeatedPendingTargets.GetValue(repeatedMergeRequest)));

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickupSlotState(
                        secondPickerId,
                        EquipmentIndex.Weapon0,
                        itemObjectId,
                        null,
                        null,
                        secondOwnerSlotWeapon.RawDataForNetwork,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        request.RequestId,
                        worldItemId,
                        stateRevision: 0,
                        responderControllerId: "observer"));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                for (short pickupNumber = 18; pickupNumber <= 26; pickupNumber++)
                {
                    Guid pickupId = Guid.NewGuid();
                    pickupIds.Add(pickupId);
                    messageBroker.Publish(
                        this,
                        new NetworkWeaponPickedup(
                            secondPickerId,
                            EquipmentIndex.Weapon0,
                            worldItemId,
                            itemObjectId,
                            null,
                            null,
                            default,
                            0,
                            pickupNumber,
                            1,
                            (short)(pickupNumber - 1),
                            false,
                            pickupId: pickupId));
                }
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                request = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropResyncRequest>());
                Assert.True(pickupIds.SetEquals(request.RequiredPickupIds));
                network.NetworkSentMessages.Clear();
                Assert.True(agentRegistry.RemoveAgent(secondPickerId));
                pickupHandler.Tick(1f);

                FieldInfo requestsAfterResponseField = typeof(WeaponPickupHandler).GetField(
                    "latestResyncRequests",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(requestsAfterResponseField);
                var requestsAfterResponse = Assert.IsAssignableFrom<System.Collections.IDictionary>(
                    requestsAfterResponseField.GetValue(pickupHandler));
                object requestAfterResponse = requestsAfterResponse[worldItemId];
                PropertyInfo pendingTargetsProperty = requestAfterResponse.GetType().GetProperty(
                    "PendingTargets",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.NotNull(pendingTargetsProperty);
                Assert.Contains(
                    (secondPickerId, EquipmentIndex.Weapon0),
                    Assert.IsAssignableFrom<
                        IEnumerable<(Guid AgentId, EquipmentIndex Slot)>>(
                        pendingTargetsProperty.GetValue(requestAfterResponse)));

                MissionWeapon delayedWorldWeapon = CreateWeapon(itemObject);
                delayedWorldWeapon.Amount = 1;
                SpawnedItemEntity delayedWorldItem =
                    new RecordingWorldItemSpawner().AddPresent(delayedWorldWeapon);
                worldItemRegistry.Register(worldItemId, delayedWorldItem);
                messageBroker.Publish(
                    this,
                    new WorldItemIdentityResolved(delayedWorldItem, worldItemId));

                Assert.Equal(20, picker.Equipment[EquipmentIndex.Weapon0].Amount);
                Assert.Equal(30, secondPicker.Equipment[EquipmentIndex.Weapon0].Amount);

                network.NetworkSentMessages.Clear();
                pickupHandler.Tick(5f);
                NetworkWeaponDropResyncRequest retry = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropResyncRequest>());
                Assert.Equal(request.RequestId, retry.RequestId);

                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropStateResponse(
                        request.RequestId,
                        worldItemId,
                        stateRevision: 1,
                        worldItemConsumed: true,
                        drop: null,
                        includedPickupIds: request.RequiredPickupIds));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                FieldInfo activeRequestsField = typeof(WeaponPickupHandler).GetField(
                    "latestResyncRequests",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(activeRequestsField);
                var activeRequests = Assert.IsAssignableFrom<System.Collections.IDictionary>(
                    activeRequestsField.GetValue(pickupHandler));
                object activeRequest = activeRequests[worldItemId];
                PropertyInfo requiredIdsProperty = activeRequest.GetType().GetProperty(
                    "RequiredPickupIds",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.NotNull(requiredIdsProperty);
                Assert.Empty(
                    Assert.IsAssignableFrom<System.Collections.IEnumerable>(
                        requiredIdsProperty.GetValue(activeRequest)));

                network.NetworkSentMessages.Clear();
                for (int i = 0; i < 5; i++)
                    pickupHandler.Tick(1f);
                Assert.Equal(
                    4,
                    network.NetworkSentMessages
                        .GetMessages<NetworkWeaponDropResyncRequest>()
                        .Count());
                network.NetworkSentMessages.Clear();
                pickupHandler.Tick(1f);
                Assert.Empty(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropResyncRequest>());

                foreach (string fieldName in new[]
                         {
                             "latestResyncRequests",
                             "appliedWorldStateRevisions",
                             "appliedSlotStateRevisions",
                         })
                {
                    FieldInfo field = typeof(WeaponPickupHandler).GetField(
                        fieldName,
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.NotNull(field);
                    Assert.Empty(
                        Assert.IsAssignableFrom<System.Collections.IDictionary>(
                            field.GetValue(pickupHandler)));
                }
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void SlotRepair_HigherAuthorityRevisionSupersedesEarlierResponse()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Guid worldItemId = Guid.NewGuid();
                Guid pickerId = Guid.NewGuid();
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon initialWeapon = CreateWeapon(itemObject);
                initialWeapon.Amount = 1;
                MissionWeapon currentWeapon = CreateWeapon(itemObject);
                currentWeapon.Amount = 20;
                AgentEquipmentShim.Track(picker, CreateEquipment(initialWeapon));
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                network.NetworkSentMessages.Clear();
                using var messageBroker = new MessageBroker();
                using var pickupHandler = new WeaponPickupHandler(
                    agentRegistry,
                    new NetworkWorldItemRegistry(),
                    network,
                    messageBroker,
                    objectManager,
                    observer.Resolve<GameInterface.Services.Entity.IControllerIdProvider>());

                var pickupIds = new HashSet<Guid>();
                for (short pickupNumber = 1; pickupNumber <= 9; pickupNumber++)
                {
                    Guid pickupId = Guid.NewGuid();
                    pickupIds.Add(pickupId);
                    messageBroker.Publish(
                        this,
                        new NetworkWeaponPickedup(
                            pickerId,
                            EquipmentIndex.Weapon0,
                            worldItemId,
                            itemObjectId,
                            null,
                            null,
                            default,
                            0,
                            pickupNumber,
                            1,
                            (short)(pickupNumber - 1),
                            false,
                            pickupId: pickupId));
                }
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                NetworkWeaponDropResyncRequest request = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropResyncRequest>());
                Assert.True(pickupIds.SetEquals(request.RequiredPickupIds));

                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropStateResponse(
                        request.RequestId,
                        worldItemId,
                        stateRevision: 1,
                        worldItemConsumed: true,
                        drop: null,
                        includedPickupIds: request.RequiredPickupIds));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.Equal(1, picker.Equipment[EquipmentIndex.Weapon0].Amount);

                MissionWeapon oldWeapon = CreateWeapon(itemObject);
                oldWeapon.Amount = 5;
                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickupSlotState(
                        pickerId,
                        EquipmentIndex.Weapon0,
                        itemObjectId,
                        null,
                        null,
                        oldWeapon.RawDataForNetwork,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        request.RequestId,
                        worldItemId,
                        stateRevision: 0,
                        responderControllerId: "observer"));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.Equal(5, picker.Equipment[EquipmentIndex.Weapon0].Amount);

                network.NetworkSentMessages.Clear();
                pickupHandler.Tick(1f);
                NetworkWeaponDropResyncRequest graceRetry = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropResyncRequest>());
                Assert.Equal(request.RequestId, graceRetry.RequestId);
                Assert.Equal(new[] { pickerId }, graceRetry.AgentIds);

                messageBroker.Publish(
                    this,
                    new NetworkWeaponPickupSlotState(
                        pickerId,
                        EquipmentIndex.Weapon0,
                        itemObjectId,
                        null,
                        null,
                        currentWeapon.RawDataForNetwork,
                        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                        request.RequestId,
                        worldItemId,
                        stateRevision: 1,
                        responderControllerId: "new-owner"));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Equal(20, picker.Equipment[EquipmentIndex.Weapon0].Amount);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void ResyncRequest_ReturnsOwnedWorldItemAndAgentSlotState()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out _);
            try
            {
                Agent dropper = ObjectHelper.SkipConstructor<Agent>();
                Agent picker = ObjectHelper.SkipConstructor<Agent>();
                MissionWeapon droppedWeapon = CreateWeapon(itemObject);
                MissionWeapon pickedWeapon = CreateWeapon(itemObject);
                pickedWeapon.Amount = 2;
                AgentEquipmentShim.Track(dropper, CreateEquipment(default));
                AgentEquipmentShim.Track(picker, CreateEquipment(pickedWeapon));

                Guid dropperId = Guid.NewGuid();
                Guid pickerId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                Assert.True(agentRegistry.TryRegisterAgent("observer", dropperId, dropper));
                Assert.True(agentRegistry.TryRegisterAgent("observer", pickerId, picker));

                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                var spawner = new RecordingWorldItemSpawner();
                SpawnedItemEntity worldItem = spawner.AddPresent(droppedWeapon);
                worldItem.Id = new MissionObjectId(93, true);
                using var messageBroker = new MessageBroker();
                var controllerIdProvider =
                    observer.Resolve<GameInterface.Services.Entity.IControllerIdProvider>();
                using var dropHandler = CreateHandler(
                    observer,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    spawner,
                    messageBroker,
                    controllerIdProvider);
                bool isLocalHost = false;
                dropHandler.ConfigureLocalHostProvider(() => isLocalHost);

                messageBroker.Publish(
                    this,
                    new WeaponDropped(
                        dropper,
                        EquipmentIndex.Weapon0,
                        droppedWeapon,
                        worldItem));
                Assert.True(worldItemRegistry.TryGetId(worldItem, out Guid worldItemId));
                network.NetworkSentMessages.Clear();

                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        Guid.NewGuid()));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.Empty(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());

                isLocalHost = true;
                network.NetworkSentMessages.Clear();
                Assert.True(agentRegistry.RemoveAgent(dropperId));

                Guid requestId = Guid.NewGuid();
                Guid requiredPickupId = Guid.NewGuid();
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        new[] { pickerId },
                        new[] { EquipmentIndex.Weapon0 },
                        requestId,
                        new[] { requiredPickupId }));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.Empty(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());

                messageBroker.Publish(
                    this,
                    new WeaponPickupApplied(
                        pickerId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        resultingWorldItemAmount: 1,
                        worldItemConsumed: false,
                        pickupId: requiredPickupId));

                NetworkWeaponDropStateResponse response = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.Equal(requestId, response.RequestId);
                Assert.False(response.WorldItemConsumed);
                Assert.True(response.Drop.IsCatchUp);
                Assert.Equal(worldItemId, response.Drop.WorldItemId);
                NetworkWeaponPickupSlotState slotState = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponPickupSlotState>());
                Assert.Equal(requestId, slotState.RequestId);
                Assert.Equal(pickerId, slotState.AgentId);
                Assert.Equal(EquipmentIndex.Weapon0, slotState.EquipmentIndex);
                Assert.Equal(itemObject, picker.Equipment[EquipmentIndex.Weapon0].Item);
                Assert.Equal(pickedWeapon.RawDataForNetwork, slotState.DataValue);

                NetworkWeaponDropStateResponse localResponse = null;
                Action<MessagePayload<NetworkWeaponDropStateResponse>> captureLocalResponse =
                    payload => localResponse = payload.What;
                messageBroker.Subscribe(captureLocalResponse);
                network.NetworkSentMessages.Clear();
                Guid localRequestId = Guid.NewGuid();
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        controllerIdProvider.ControllerId,
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        localRequestId));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.Empty(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.NotNull(localResponse);
                Assert.Equal(localRequestId, localResponse.RequestId);

                network.NetworkSentMessages.Clear();
                Guid consumedRequestId = Guid.NewGuid();
                messageBroker.Publish(
                    this,
                    new WeaponPickupApplied(
                        pickerId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        resultingWorldItemAmount: 0,
                        worldItemConsumed: true,
                        pickupId: Guid.NewGuid()));
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        consumedRequestId));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                NetworkWeaponDropStateResponse consumedResponse = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.Equal(consumedRequestId, consumedResponse.RequestId);
                Assert.True(consumedResponse.WorldItemConsumed);
                Assert.Null(consumedResponse.Drop);
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void UnresolvedPreDropState_IsBoundedAndExpiresAsOneRecord()
    {
        RunWithAgentShims(observer =>
        {
            using var messageBroker = new MessageBroker();
            using var handler = new WeaponDropHandler(
                observer.Resolve<INetworkAgentRegistry>(),
                new NetworkWorldItemRegistry(),
                messageBroker,
                observer.Resolve<IBattleNetwork>(),
                observer.Resolve<IObjectManager>(),
                new RecordingWorldItemSpawner());
            handler.ConfigureLocalHostProvider(() => true);

            MethodInfo getOrCreate = typeof(WeaponDropHandler).GetMethod(
                "GetOrCreateWorldItemTransitionState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(getOrCreate);
            for (int i = 0; i < 257; i++)
                getOrCreate.Invoke(handler, new object[] { Guid.NewGuid() });

            FieldInfo statesField = typeof(WeaponDropHandler).GetField(
                "worldItemTransitionStates",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(statesField);
            var states = Assert.IsAssignableFrom<System.Collections.IDictionary>(
                statesField.GetValue(handler));
            Assert.Equal(256, states.Count);
            FieldInfo stateOrderField = typeof(WeaponDropHandler).GetField(
                "preDropStateOrder",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(stateOrderField);
            var stateOrder = Assert.IsAssignableFrom<System.Collections.ICollection>(
                stateOrderField.GetValue(handler));
            Assert.Equal(256, stateOrder.Count);

            object expiringState = states.Values.Cast<object>().First();
            PropertyInfo expiresProperty = expiringState.GetType().GetProperty(
                "ExpiresAtUtc",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(expiresProperty);
            expiresProperty.SetValue(expiringState, DateTime.MinValue);

            handler.Tick(1f);

            Assert.Equal(255, states.Count);
            Assert.Equal(255, stateOrder.Count);

            MethodInfo markLive = typeof(WeaponDropHandler).GetMethod(
                "MarkLiveDropApplied",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(markLive);
            foreach (Guid worldItemId in states.Keys.Cast<Guid>().ToArray())
                markLive.Invoke(handler, new object[] { worldItemId });
            Assert.Equal(0, stateOrder.Count);

            Guid pendingRequestId = Guid.NewGuid();
            messageBroker.Publish(
                this,
                new NetworkWeaponDropResyncRequest(
                    Guid.NewGuid(),
                    "requester",
                    Array.Empty<Guid>(),
                    Array.Empty<EquipmentIndex>(),
                    pendingRequestId));
            Common.GameThread.Instance.Update(TimeSpan.Zero);

            FieldInfo requestsField = typeof(WeaponDropHandler).GetField(
                "pendingResyncRequests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo requestOrderField = typeof(WeaponDropHandler).GetField(
                "pendingResyncRequestOrder",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(requestsField);
            Assert.NotNull(requestOrderField);
            var requests = Assert.IsAssignableFrom<System.Collections.IDictionary>(
                requestsField.GetValue(handler));
            var requestOrder = Assert.IsAssignableFrom<System.Collections.ICollection>(
                requestOrderField.GetValue(handler));
            Assert.Equal(1, requests.Count);
            Assert.Equal(1, requestOrder.Count);

            object pendingRequest = requests[pendingRequestId];
            FieldInfo requestExpiry = pendingRequest.GetType().GetField(
                "<ExpiresAtUtc>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(requestExpiry);
            requestExpiry.SetValue(pendingRequest, DateTime.MinValue);
            handler.Tick(1f);

            Assert.Equal(0, requests.Count);
            Assert.Equal(0, requestOrder.Count);
        });
    }

    [Fact]
    public void ResyncResponder_AcknowledgesAll513ConsumedPickupIds()
    {
        RunWithAgentShims(observer =>
        {
            var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
            network.RouteMessages = false;
            network.NetworkSentMessages.Clear();
            using var messageBroker = new MessageBroker();
            using var handler = new WeaponDropHandler(
                observer.Resolve<INetworkAgentRegistry>(),
                new NetworkWorldItemRegistry(),
                messageBroker,
                network,
                observer.Resolve<IObjectManager>(),
                new RecordingWorldItemSpawner());
            handler.ConfigureLocalHostProvider(() => true);
            Guid worldItemId = Guid.NewGuid();
            Guid agentId = Guid.NewGuid();
            var pickupIds = new Guid[513];
            for (int i = 0; i < pickupIds.Length; i++)
            {
                pickupIds[i] = Guid.NewGuid();
                messageBroker.Publish(
                    this,
                    new WeaponPickupApplied(
                        agentId,
                        EquipmentIndex.Weapon0,
                        worldItemId,
                        resultingWorldItemAmount: (short)Math.Max(0, 512 - i),
                        worldItemConsumed: i == pickupIds.Length - 1,
                        pickupId: pickupIds[i]));
            }

            Guid requestId = Guid.NewGuid();
            foreach (Guid[] requiredIds in new[]
                     {
                         pickupIds.Take(512).ToArray(),
                         pickupIds.Skip(512).ToArray(),
                     })
            {
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        requestId,
                        requiredIds));
            }
            Common.GameThread.Instance.Update(TimeSpan.Zero);

            NetworkWeaponDropStateResponse[] responses = network.NetworkSentMessages
                .GetMessages<NetworkWeaponDropStateResponse>()
                .ToArray();
            Assert.Equal(2, responses.Length);
            Assert.All(responses, response => Assert.Equal(requestId, response.RequestId));
            Assert.All(responses, response => Assert.True(response.WorldItemConsumed));
            Assert.All(
                responses,
                response => Assert.InRange(response.IncludedPickupIds.Length, 0, 512));
            Assert.Equal(pickupIds.Take(512), responses[0].IncludedPickupIds);
            Assert.Equal(pickupIds.Skip(512), responses[1].IncludedPickupIds);
            Assert.True(
                new HashSet<Guid>(pickupIds).IsSubsetOf(
                    responses.SelectMany(response => response.IncludedPickupIds)));
        });
    }

    [Fact]
    public void JoinCatchUp_TransfersPendingPickupStateAndConsumedTombstones()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Guid activeWorldItemId = Guid.NewGuid();
                Guid activePickupId = Guid.NewGuid();
                Guid consumedWorldItemId = Guid.NewGuid();
                Guid consumedPickupId = Guid.NewGuid();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                network.NetworkSentMessages.Clear();

                using (var hostBroker = new MessageBroker())
                using (var host = new WeaponDropHandler(
                           observer.Resolve<INetworkAgentRegistry>(),
                           new NetworkWorldItemRegistry(),
                           hostBroker,
                           network,
                           objectManager,
                           new RecordingWorldItemSpawner()))
                {
                    host.ConfigureLocalHostProvider(() => true);
                    hostBroker.Publish(
                        this,
                        new WeaponPickupApplied(
                            Guid.NewGuid(),
                            EquipmentIndex.Weapon0,
                            activeWorldItemId,
                            resultingWorldItemAmount: 4,
                            worldItemConsumed: false,
                            pickupId: activePickupId));
                    hostBroker.Publish(
                        this,
                        new WeaponPickupApplied(
                            Guid.NewGuid(),
                            EquipmentIndex.Weapon0,
                            consumedWorldItemId,
                            resultingWorldItemAmount: 0,
                            worldItemConsumed: true,
                            pickupId: consumedPickupId));

                    host.CatchUpJoiner("joiner");
                    Common.GameThread.Instance.Update(TimeSpan.Zero);
                }

                NetworkWeaponDropStateResponse[] catchUpStates = network.NetworkSentMessages
                    .GetMessages<NetworkWeaponDropStateResponse>()
                    .ToArray();
                Assert.Equal(2, catchUpStates.Length);
                NetworkWeaponDropStateResponse activeState = Assert.Single(
                    catchUpStates,
                    state => state.WorldItemId == activeWorldItemId);
                Assert.Equal(Guid.Empty, activeState.RequestId);
                Assert.True(activeState.HasRemainingAmount);
                Assert.Equal(4, activeState.RemainingAmount);
                Assert.Equal(new[] { activePickupId }, activeState.IncludedPickupIds);
                NetworkWeaponDropStateResponse consumedState = Assert.Single(
                    catchUpStates,
                    state => state.WorldItemId == consumedWorldItemId);
                Assert.True(consumedState.WorldItemConsumed);
                Assert.Equal(new[] { consumedPickupId }, consumedState.IncludedPickupIds);

                network.NetworkSentMessages.Clear();
                var joinerRegistry = new NetworkWorldItemRegistry();
                var joinerSpawner = new RecordingWorldItemSpawner();
                using var joinerBroker = new MessageBroker();
                using var joiner = new WeaponDropHandler(
                    observer.Resolve<INetworkAgentRegistry>(),
                    joinerRegistry,
                    joinerBroker,
                    network,
                    objectManager,
                    joinerSpawner);
                foreach (NetworkWeaponDropStateResponse state in catchUpStates)
                    joinerBroker.Publish(this, state);
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                MissionWeapon sourceWeapon = CreateWeapon(itemObject);
                sourceWeapon.Amount = 10;
                joinerBroker.Publish(
                    this,
                    CreateDropMessage(
                        Guid.NewGuid(),
                        activeWorldItemId,
                        itemObjectId,
                        sourceWeapon));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(joinerRegistry.TryGet(
                    activeWorldItemId,
                    out SpawnedItemEntity remainingItem));
                Assert.Equal(4, remainingItem.WeaponCopy.Amount);

                joiner.ConfigureLocalHostProvider(() => true);
                network.NetworkSentMessages.Clear();
                Guid activeRequestId = Guid.NewGuid();
                joinerBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        activeWorldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        activeRequestId,
                        new[] { activePickupId }));
                Guid consumedRequestId = Guid.NewGuid();
                joinerBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        consumedWorldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        consumedRequestId,
                        new[] { consumedPickupId }));
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                NetworkWeaponDropStateResponse[] repaired = network.NetworkSentMessages
                    .GetMessages<NetworkWeaponDropStateResponse>()
                    .ToArray();
                Assert.Equal(2, repaired.Length);
                Assert.Contains(
                    repaired,
                    response => response.RequestId == activeRequestId &&
                        response.HasRemainingAmount &&
                        response.RemainingAmount == 4 &&
                        response.IncludedPickupIds.Contains(activePickupId));
                Assert.Contains(
                    repaired,
                    response => response.RequestId == consumedRequestId &&
                        response.WorldItemConsumed &&
                        response.IncludedPickupIds.Contains(consumedPickupId));
            }
            finally
            {
                objectManager.Remove(itemObject);
            }
        });
    }

    [Fact]
    public void MissingLiveWorldItem_RemainsAvailableForCatchUpAndRepair()
    {
        RunWithAgentShims(observer =>
        {
            var objectManager = observer.Resolve<IObjectManager>();
            ItemObject itemObject = RegisterItem(objectManager, out string itemObjectId);
            try
            {
                Guid worldItemId = Guid.NewGuid();
                var worldItemRegistry = new NetworkWorldItemRegistry();
                var network = Assert.IsType<MockBattleNetwork>(observer.Resolve<IBattleNetwork>());
                network.RouteMessages = false;
                var spawner = new RecordingWorldItemSpawner();
                using var messageBroker = new MessageBroker();
                using var handler = new WeaponDropHandler(
                    observer.Resolve<INetworkAgentRegistry>(),
                    worldItemRegistry,
                    messageBroker,
                    network,
                    objectManager,
                    spawner);

                NetworkWeaponDropped drop = CreateDropMessage(
                    Guid.NewGuid(),
                    worldItemId,
                    itemObjectId,
                    CreateWeapon(itemObject));
                messageBroker.Publish(this, drop);
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity item));
                Assert.True(spawner.TryRemove(item));

                network.NetworkSentMessages.Clear();
                handler.CatchUpJoiner("later-joiner");
                NetworkWeaponDropped catchUp = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
                Assert.True(catchUp.IsCatchUp);
                Assert.Equal(worldItemId, catchUp.WorldItemId);
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));

                handler.Tick(1f);

                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
                messageBroker.Publish(this, catchUp);
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity repairedItem));
                Assert.Equal(2, spawner.SpawnCount);

                FieldInfo expiryField = typeof(WeaponDropHandler).GetField(
                    "activeDropRemainingLifeTime",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(expiryField);
                var remainingLifeTimes = Assert.IsType<Dictionary<Guid, float>>(
                    expiryField.GetValue(handler));

                remainingLifeTimes[worldItemId] = 0f;
                handler.ConfigureLocalHostProvider(() => true);
                network.NetworkSentMessages.Clear();
                handler.Tick(1f);
                handler.CatchUpJoiner("live-item-joiner");

                Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
                Assert.True(worldItemRegistry.TryGet(worldItemId, out _));

                network.NetworkSentMessages.Clear();
                Guid liveRequestId = Guid.NewGuid();
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        liveRequestId));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                NetworkWeaponDropStateResponse liveResponse = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.Equal(liveRequestId, liveResponse.RequestId);
                Assert.False(liveResponse.WorldItemConsumed);
                Assert.True(worldItemRegistry.TryGet(worldItemId, out _));

                Assert.True(spawner.TryRemove(repairedItem));
                const float earlierRemainingLifeTime = 60f;
                remainingLifeTimes[worldItemId] = earlierRemainingLifeTime;
                messageBroker.Publish(this, catchUp);
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                Assert.True(worldItemRegistry.TryGet(
                    worldItemId,
                    out SpawnedItemEntity preExpiryRepair));
                Assert.Equal(3, spawner.SpawnCount);
                Assert.Equal(earlierRemainingLifeTime, remainingLifeTimes[worldItemId]);

                Assert.True(spawner.TryRemove(preExpiryRepair));
                Guid activeRequestId = Guid.NewGuid();
                network.NetworkSentMessages.Clear();
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        activeRequestId));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                NetworkWeaponDropStateResponse activeResponse = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.False(activeResponse.WorldItemConsumed);

                Guid requestId = Guid.NewGuid();
                Guid requiredPickupId = Guid.NewGuid();
                network.NetworkSentMessages.Clear();
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        requestId,
                        new[] { requiredPickupId }));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                Assert.Empty(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());

                remainingLifeTimes[worldItemId] = 0f;
                messageBroker.Publish(this, catchUp);
                Common.GameThread.Instance.Update(TimeSpan.Zero);

                NetworkWeaponDropStateResponse expiredResponse = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.Equal(requestId, expiredResponse.RequestId);
                Assert.True(expiredResponse.WorldItemConsumed);
                Assert.Null(expiredResponse.Drop);
                Assert.Equal(new[] { requiredPickupId }, expiredResponse.IncludedPickupIds);
                Assert.Equal(3, spawner.SpawnCount);

                network.NetworkSentMessages.Clear();
                messageBroker.Publish(
                    this,
                    new NetworkWeaponDropResyncRequest(
                        worldItemId,
                        "requester",
                        Array.Empty<Guid>(),
                        Array.Empty<EquipmentIndex>(),
                        activeRequestId));
                Common.GameThread.Instance.Update(TimeSpan.Zero);
                NetworkWeaponDropStateResponse terminalRetry = Assert.Single(
                    network.NetworkSentMessages.GetMessages<NetworkWeaponDropStateResponse>());
                Assert.Equal(activeRequestId, terminalRetry.RequestId);
                Assert.True(terminalRetry.WorldItemConsumed);
                Assert.True(terminalRetry.StateRevision > activeResponse.StateRevision);

                network.NetworkSentMessages.Clear();
                handler.Tick(1f);
                handler.CatchUpJoiner("expired-joiner");

                Assert.Empty(network.NetworkSentMessages.GetMessages<NetworkWeaponDropped>());
                Assert.False(worldItemRegistry.TryGet(worldItemId, out _));
                FieldInfo activeDropsField = typeof(WeaponDropHandler).GetField(
                    "activeDrops",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(activeDropsField);
                Assert.Empty(
                    Assert.IsAssignableFrom<System.Collections.IDictionary>(
                        activeDropsField.GetValue(handler)));
                FieldInfo pendingRequestsField = typeof(WeaponDropHandler).GetField(
                    "pendingResyncRequests",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(pendingRequestsField);
                Assert.Empty(
                    Assert.IsAssignableFrom<System.Collections.IDictionary>(
                        pendingRequestsField.GetValue(handler)));
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
        IMessageBroker messageBroker,
        GameInterface.Services.Entity.IControllerIdProvider controllerIdProvider = null) =>
        new WeaponDropHandler(
            agentRegistry,
            worldItemRegistry,
            messageBroker,
            observer.Resolve<IBattleNetwork>(),
            objectManager,
            spawner,
            controllerIdProvider);

    private static NetworkWeaponDropped CreateDropMessage(
        Guid agentId,
        Guid worldItemId,
        string itemObjectId,
        MissionWeapon weapon,
        EquipmentIndex equipmentIndex = EquipmentIndex.Weapon0,
        bool isCatchUp = false,
        AgentEquipmentData? currentEquipment = null,
        Guid? dropId = null) =>
        new NetworkWeaponDropped(
            dropId ?? worldItemId,
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

    private static WeaponDropHandler GetWeaponDropHandler(CoopMissionController controller)
    {
        FieldInfo componentField = typeof(CoopMissionController).GetField(
            "coopMissionComponent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentField);
        var component = Assert.IsAssignableFrom<ICoopMissionComponent>(
            componentField.GetValue(controller));
        return Assert.IsType<WeaponDropHandler>(component.WeaponDropHandler);
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

    private static void ExpireWorldItemTransitionState(
        WeaponDropHandler handler,
        Guid worldItemId)
    {
        FieldInfo statesField = typeof(WeaponDropHandler).GetField(
            "worldItemTransitionStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(statesField);
        var states = Assert.IsAssignableFrom<System.Collections.IDictionary>(
            statesField.GetValue(handler));
        object state = states[worldItemId];
        Assert.NotNull(state);
        PropertyInfo expiresProperty = state.GetType().GetProperty(
            "ExpiresAtUtc",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(expiresProperty);
        expiresProperty.SetValue(state, DateTime.MinValue);
    }

    private static int GetPendingObservedDropCount(
        WeaponDropHandler handler,
        Guid agentId,
        EquipmentIndex equipmentIndex)
    {
        FieldInfo pendingField = typeof(WeaponDropHandler).GetField(
            "pendingDrops",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(pendingField);
        var pending = Assert.IsAssignableFrom<System.Collections.IDictionary>(
            pendingField.GetValue(handler));
        object key = (agentId, equipmentIndex);
        if (!pending.Contains(key)) return 0;

        object queue = pending[key];
        return (int)queue.GetType().GetProperty("Count").GetValue(queue);
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
                typeof(AgentEquipmentData),
                nameof(AgentEquipmentData.Apply),
                new[] { typeof(Agent) }),
            prefix: Prefix(nameof(AgentEquipmentShim.SkipEquipmentApply)));
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
        private readonly List<SpawnedItemEntity> presentItems = new();

        public int SpawnCount { get; private set; }
        public SpawnedItemEntity LastSpawnedItem { get; private set; }
        public bool FailRemovals { get; set; }
        public int PresentCount => presentItems.Count;

        public SpawnedItemEntity AddPresent(MissionWeapon weapon)
        {
            SpawnedItemEntity item = ObjectHelper.SkipConstructor<SpawnedItemEntity>();
            item._weapon = weapon;
            presentItems.Add(item);
            return item;
        }

        public bool IsPresent(SpawnedItemEntity item) =>
            item != null && presentItems.Any(presentItem => ReferenceEquals(presentItem, item));

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

        public bool TryRemove(SpawnedItemEntity item)
        {
            if (item == null) return true;
            if (FailRemovals) return false;

            int index = presentItems.FindIndex(
                presentItem => ReferenceEquals(presentItem, item));
            if (index < 0) return false;

            presentItems.RemoveAt(index);
            return true;
        }
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

        public static bool SkipEquipmentApply() => false;

        public static bool DropItem(Agent __instance, EquipmentIndex itemIndex)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            state.DropCount++;
            state.Equipment[itemIndex] = default;
            return false;
        }
    }
}
