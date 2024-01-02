using Common.Logging.Attributes;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Updates <see cref="MobilePartyAi"/> behavior on the campaign map.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[BatchLogMessage]
public struct UpdatePartyBehavior : ICommand
{
    public PartyBehaviorUpdateData BehaviorUpdateData;

    public UpdatePartyBehavior(ref PartyBehaviorUpdateData behaviorUpdateData)
    {
        BehaviorUpdateData = behaviorUpdateData;
    }
}

/// <summary>
/// Notifies that PartyBehavior was updated
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[BatchLogMessage]
public struct PartyBehaviorUpdated : IEvent
{
    public PartyBehaviorUpdateData BehaviorUpdateData { get; }
    public PartyBehaviorUpdated(ref PartyBehaviorUpdateData behaviorUpdateData)
    {
        BehaviorUpdateData = behaviorUpdateData;
    }
}