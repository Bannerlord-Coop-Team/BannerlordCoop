using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace Coop.Core.Client.Services.Armies.Messages;

/// <summary>
/// Server sends this data when a Army called OnRemovePartyInternal
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRemoveMobilePartyInArmy : ICommand
{
    [ProtoMember(1)]
    public List<string> MobilePartyIds { get; }
    [ProtoMember(2)]
    public string ArmyId { get; }

    public NetworkRemoveMobilePartyInArmy(List<string> mobilePartyIds, string armyId)
    {
        MobilePartyIds = mobilePartyIds;
        ArmyId = armyId;
    }
}
