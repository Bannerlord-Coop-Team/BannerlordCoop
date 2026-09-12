using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyRelationsIncreasedWithNotables : IEvent
{
    public readonly Dictionary<Hero, (bool, bool)> PlayerSettlementOwnerRelationsChanges;

    public NotifyRelationsIncreasedWithNotables(
        Dictionary<Hero, (bool, bool)> playerSettlementOwnerRelationsChanges)
    {
        PlayerSettlementOwnerRelationsChanges = playerSettlementOwnerRelationsChanges;
    }
}
