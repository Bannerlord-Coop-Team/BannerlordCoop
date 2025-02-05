using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify clients of <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Gold"/> set
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkChangeSettlementComponentGold : ICommand
    {
        [ProtoMember(1)]
        public string SettlementComponentId { get; }
        [ProtoMember(2)]
        public int Gold { get; }
        public NetworkChangeSettlementComponentGold(string settlementComponentId, int gold)
        {
            SettlementComponentId = settlementComponentId;
            Gold = gold;
        }
    }
}
