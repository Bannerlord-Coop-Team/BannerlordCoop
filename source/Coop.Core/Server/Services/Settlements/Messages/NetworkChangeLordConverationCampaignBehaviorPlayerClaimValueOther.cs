using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notifies client to change Settlement.ClaimValue
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeLordConverationCampaignBehaviorPlayerClaimValueOther : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public float ClaimValue { get; }

    public NetworkChangeLordConverationCampaignBehaviorPlayerClaimValueOther(string settlementId, float claimValue)
    {
        SettlementId = settlementId;
        ClaimValue = claimValue;
    }
}
