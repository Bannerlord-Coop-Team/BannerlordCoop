using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkNotifyRelationsIncreasedWithNotables : IEvent
{
    [ProtoMember(1)]
    public readonly Dictionary<string, (bool, bool)> PlayerIdSettlementOwnerRelationsChanges;

    public NetworkNotifyRelationsIncreasedWithNotables(
        Dictionary<string, (bool, bool)> playerIdSettlementOwnerRelationsChanges)
    {
        PlayerIdSettlementOwnerRelationsChanges = playerIdSettlementOwnerRelationsChanges;
    }
}
