using Common.Messaging;
using GameInterface.Services.Armies.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace Coop.Core.Client.Services.Armies.Messages;

/// <summary>
/// Server sends this data when a Army called OnRemovePartyInternal
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRemovePartyInArmy : ICommand
{
    [ProtoMember(1)]
    public ArmyRemovePartyData Data { get; }

    public NetworkRemovePartyInArmy(ArmyRemovePartyData data)
    {
        Data = data;
    }
}
