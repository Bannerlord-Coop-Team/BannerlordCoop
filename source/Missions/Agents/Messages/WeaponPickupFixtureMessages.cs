#if DEBUG
using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages
{
    internal sealed class PreparePartialConsumablePickupFixture : IEvent
    {
        public Agent Agent { get; }
        public bool Handled { get; set; }
        public bool Succeeded { get; set; }
        public string Error { get; set; }
        public EquipmentIndex SourceSlot { get; set; }
        public EquipmentIndex DropSlot { get; set; }
        public string ItemObjectId { get; set; }
        public short OriginalSourceAmount { get; set; }
        public short SourceAmount { get; set; }
        public short DroppedAmount { get; set; }

        public PreparePartialConsumablePickupFixture(Agent agent)
        {
            Agent = agent;
        }
    }

    internal sealed class RestorePartialConsumablePickupFixture : IEvent
    {
        public Agent Agent { get; }
        public Guid AgentId { get; }
        public Guid WorldItemId { get; }
        public bool Handled { get; set; }
        public bool Succeeded { get; set; }
        public string Error { get; set; }
        public short RestoredSourceAmount { get; set; }
        public bool DropSlotEmpty { get; set; }
        public bool WorldItemInactive { get; set; }

        public RestorePartialConsumablePickupFixture(
            Agent agent,
            Guid agentId,
            Guid worldItemId)
        {
            Agent = agent;
            AgentId = agentId;
            WorldItemId = worldItemId;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    internal sealed class NetworkPreparePartialConsumablePickupFixture : IEvent
    {
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex SourceSlot { get; }

        [ProtoMember(3)]
        public EquipmentIndex DropSlot { get; }

        [ProtoMember(4)]
        public string ItemObjectId { get; }

        [ProtoMember(5)]
        public short SourceAmount { get; }

        [ProtoMember(6)]
        public short DroppedAmount { get; }

        public NetworkPreparePartialConsumablePickupFixture(
            Guid agentId,
            EquipmentIndex sourceSlot,
            EquipmentIndex dropSlot,
            string itemObjectId,
            short sourceAmount,
            short droppedAmount)
        {
            AgentId = agentId;
            SourceSlot = sourceSlot;
            DropSlot = dropSlot;
            ItemObjectId = itemObjectId;
            SourceAmount = sourceAmount;
            DroppedAmount = droppedAmount;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    internal sealed class NetworkRestorePartialConsumablePickupFixture : IEvent
    {
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public Guid WorldItemId { get; }

        public NetworkRestorePartialConsumablePickupFixture(Guid agentId, Guid worldItemId)
        {
            AgentId = agentId;
            WorldItemId = worldItemId;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    internal sealed class NetworkTriggerEmptyExtraSlotWeaponDrop : IEvent
    {
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public Guid WorldItemId { get; }

        public NetworkTriggerEmptyExtraSlotWeaponDrop(Guid agentId, Guid worldItemId)
        {
            AgentId = agentId;
            WorldItemId = worldItemId;
        }
    }
}
#endif
