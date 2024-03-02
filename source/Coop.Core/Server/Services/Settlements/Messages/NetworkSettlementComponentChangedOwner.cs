using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify clients of <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Owner"/> set
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkSettlementComponentChangedOwner : IEvent
    {
        [ProtoMember(1)]
        public string SettlementComponentId { get; set; }
        [ProtoMember(2)]
        public string OwnerId { get; set; }
        public NetworkSettlementComponentChangedOwner(string settlementComponentId, string ownerId)
        {
            SettlementComponentId = settlementComponentId;
            OwnerId = ownerId;
        }
    }
}
