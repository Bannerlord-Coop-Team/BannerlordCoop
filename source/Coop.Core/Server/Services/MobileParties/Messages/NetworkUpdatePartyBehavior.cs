using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Sent to the client by the server when a party's AI behavior is updated.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkUpdatePartyBehavior : IMessage
{
    [ProtoMember(1)]
    public readonly PartyBehaviorUpdateData BehaviorUpdateData;

    public NetworkUpdatePartyBehavior(PartyBehaviorUpdateData behaviorUpdateData)
    {
        BehaviorUpdateData = behaviorUpdateData;
    }
}
