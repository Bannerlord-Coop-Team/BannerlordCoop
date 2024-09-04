using Common.Messaging;
using GameInterface.Services.Armies.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace Coop.Core.Client.Services.Armies.Messages;

/// <summary>
/// Server sends this data when a Army called OnAddPartyInternal
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkAddMobilePartyInArmy : ICommand
{
    [ProtoMember(1)]
    public ArmyAddPartyData Data { get; }

    public NetworkAddMobilePartyInArmy(ArmyAddPartyData data)
    {
        Data = data;
    }
}