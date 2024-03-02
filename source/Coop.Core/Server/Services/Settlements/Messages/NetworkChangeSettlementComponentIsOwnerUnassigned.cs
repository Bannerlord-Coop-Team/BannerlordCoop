using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify clients of <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.IsOwnerUnassigned"/> set
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkChangeSettlementComponentIsOwnerUnassigned : ICommand
    {
        [ProtoMember(1)]
        public string SettlementComponentId { get; }
        [ProtoMember(2)]
        public bool IsOwnerUnassigned { get; }
        public NetworkChangeSettlementComponentIsOwnerUnassigned(string settlementComponentId, bool isOwnerUnassigned)
        {
            SettlementComponentId = settlementComponentId;
            IsOwnerUnassigned = isOwnerUnassigned;
        }
    }
}
