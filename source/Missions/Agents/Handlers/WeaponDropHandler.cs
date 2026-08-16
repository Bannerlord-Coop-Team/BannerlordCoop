using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Agents.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>
/// Handler for weapon drops in a battle
/// </summary>
public interface IWeaponDropHandler : IHandler
{

}
/// <inheritdoc/>
public class WeaponDropHandler : IWeaponDropHandler
{
    readonly static ILogger Logger = LogManager.GetLogger<WeaponDropHandler>();

    private readonly INetworkAgentRegistry networkAgentRegistry;
    private readonly INetworkWorldItemRegistry worldItemRegistry;
    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;

    public WeaponDropHandler(
        INetworkAgentRegistry networkAgentRegistry,
        INetworkWorldItemRegistry worldItemRegistry,
        IMessageBroker messageBroker,
        IBattleNetwork network)
    {
        this.networkAgentRegistry = networkAgentRegistry;
        this.worldItemRegistry = worldItemRegistry;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<WeaponDropped>(WeaponDropSend);
        messageBroker.Subscribe<NetworkWeaponDropped>(WeaponDropReceive);
#if DEBUG
        messageBroker.Subscribe<NetworkTriggerEmptyExtraSlotWeaponDrop>(TriggerEmptyExtraSlotWeaponDrop);
#endif
        this.network = network;
    }

    ~WeaponDropHandler()
    {
        Dispose();
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WeaponDropped>(WeaponDropSend);
        messageBroker.Unsubscribe<NetworkWeaponDropped>(WeaponDropReceive);
#if DEBUG
        messageBroker.Unsubscribe<NetworkTriggerEmptyExtraSlotWeaponDrop>(TriggerEmptyExtraSlotWeaponDrop);
#endif
    }

    private void WeaponDropSend(MessagePayload<WeaponDropped> obj)
    {
        if (!networkAgentRegistry.IsLocallyControlled(obj.What.Agent))
            return;

        if (!networkAgentRegistry.TryGetAgentInfo(obj.What.Agent, out var agentInfo))
        {
            Logger.Warning("No agentID was found for the Agent: {agent} in {class}", obj.What.Agent, typeof(WeaponDropHandler));
            return;
        }

        Guid worldItemId = worldItemRegistry.GetOrCreateId(obj.What.DroppedItem);
        NetworkWeaponDropped message = new NetworkWeaponDropped(
            agentInfo.AgentId,
            obj.What.EquipmentIndex,
            worldItemId);

        network.SendAll(message);
    }

    private void WeaponDropReceive(MessagePayload<NetworkWeaponDropped> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!networkAgentRegistry.TryGetAgentInfo(obj.What.AgentId, out var agentInfo))
            {
                Logger.Warning("No agent found for {guid} in {class}", obj.What.AgentId, typeof(WeaponDropHandler));
                return;
            }

            var agent = agentInfo.Agent;
            EquipmentIndex equipmentIndex = obj.What.EquipmentIndex;
            if (equipmentIndex < EquipmentIndex.WeaponItemBeginSlot ||
                equipmentIndex >= EquipmentIndex.NumAllWeaponSlots ||
                agent.Equipment[equipmentIndex].IsEmpty)
            {
                Logger.Warning(
                    "Ignored weapon drop for empty or invalid slot {EquipmentIndex} on agent {AgentId}",
                    equipmentIndex,
                    obj.What.AgentId);
                return;
            }

            HashSet<SpawnedItemEntity> existingItems = WeaponDropItemTracker.Capture();
            agent.DropItem(equipmentIndex);
            SpawnedItemEntity droppedItem = WeaponDropItemTracker.FindDroppedItem(existingItems);
            worldItemRegistry.Register(obj.What.WorldItemId, droppedItem);
        });
    }

#if DEBUG
    private void TriggerEmptyExtraSlotWeaponDrop(
        MessagePayload<NetworkTriggerEmptyExtraSlotWeaponDrop> payload)
    {
        if (ModInformation.IsServer) return;

        GameThread.RunSafe(() =>
        {
            NetworkTriggerEmptyExtraSlotWeaponDrop request = payload.What;
            if (!networkAgentRegistry.TryGetAgentInfo(request.AgentId, out var agentInfo) ||
                !networkAgentRegistry.IsLocallyControlled(request.AgentId))
            {
                Logger.Warning("Ignored empty extra-slot weapon drop request for {AgentId}", request.AgentId);
                return;
            }

            Agent agent = agentInfo.Agent;
            if (!agent.IsActive() ||
                agent.Mission != Mission.Current ||
                Mission.Current?.IsSiegeBattle != true ||
                !agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty)
            {
                Logger.Warning("Ignored empty extra-slot weapon drop request for unavailable agent {AgentId}", request.AgentId);
                return;
            }

            network.SendAll(new NetworkWeaponDropped(
                request.AgentId,
                EquipmentIndex.ExtraWeaponSlot,
                request.WorldItemId));
        });
    }
#endif
}
