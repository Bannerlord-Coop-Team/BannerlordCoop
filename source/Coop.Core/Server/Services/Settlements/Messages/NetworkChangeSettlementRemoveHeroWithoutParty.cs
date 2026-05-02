using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notify clients to remove hero without party
/// </summary>
[ProtoContract(SkipConstructor = true)]
[BatchLogMessage]
public readonly struct NetworkChangeSettlementRemoveHeroWithoutParty : IEvent
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string HeroId;

    public NetworkChangeSettlementRemoveHeroWithoutParty(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
