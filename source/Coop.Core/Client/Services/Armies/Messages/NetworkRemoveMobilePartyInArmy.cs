﻿using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Armies.Messages;

/// <summary>
/// Server sends this data when a Army called OnRemovePartyInternal
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRemoveMobilePartyInArmy : ICommand
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public string LeaderMobilePartyId { get; }

    public NetworkRemoveMobilePartyInArmy(string mobilePartyId, string leaderMobilePartyId)
    {
        MobilePartyId = mobilePartyId;
        LeaderMobilePartyId = leaderMobilePartyId;
    }
}
