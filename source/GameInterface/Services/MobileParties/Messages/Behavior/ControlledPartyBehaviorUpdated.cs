using Common.Logging.Attributes;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// The behavior of a controlled party has been updated.
/// Must be confirmed by the server before the change is applied.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[BatchLogMessage]
public record ControlledPartyBehaviorUpdated : IEvent
{
    public PartyBehaviorUpdateData BehaviorUpdateData { get; }

    public ControlledPartyBehaviorUpdated(PartyBehaviorUpdateData behaviorUpdateData)
    {
        BehaviorUpdateData = behaviorUpdateData;
    }
}