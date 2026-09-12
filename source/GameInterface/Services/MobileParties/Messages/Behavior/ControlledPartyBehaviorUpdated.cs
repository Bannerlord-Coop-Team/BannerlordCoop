using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// The behavior of a controlled party has been updated.
/// Must be sent to the server for replication.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
public record ControlledPartyBehaviorUpdated : IEvent
{
    public PartyBehaviorUpdateData BehaviorUpdateData { get; }

    public ControlledPartyBehaviorUpdated(PartyBehaviorUpdateData behaviorUpdateData)
    {
        BehaviorUpdateData = behaviorUpdateData;
    }
}