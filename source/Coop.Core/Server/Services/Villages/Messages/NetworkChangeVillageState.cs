using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Villages.Messages;

/// <summary>
/// 
/// </summary>
[ProtoContract]
public record NetworkChangeVillageState : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public int State { get; }

    public NetworkChangeVillageState(string settlementId, int state)
    {
        SettlementId = settlementId;
        State = state;
    }
}
