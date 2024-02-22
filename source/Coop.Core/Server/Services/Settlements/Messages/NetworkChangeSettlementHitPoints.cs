using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Sends client the value of SettlementHitPoints
/// </summary>
[ProtoContract(SkipConstructor =true)]
public record NetworkChangeSettlementHitPoints : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public float SettlementHitPoints { get; }

    public NetworkChangeSettlementHitPoints(string settlementId, float settlementHitPoints)
    {
        SettlementId = settlementId;
        SettlementHitPoints = settlementHitPoints;
    }
}
