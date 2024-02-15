using Common.Messaging;
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
    public List<string> MobilePartyListId { get; }
    [ProtoMember(2)]
    public string ArmyId { get; }

    public NetworkAddMobilePartyInArmy(List<string> mobilePartyListId, string armyId)
    {
        MobilePartyListId = mobilePartyListId;
        ArmyId = armyId;
    }
}