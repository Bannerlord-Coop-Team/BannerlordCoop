using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Authoritative time and mobile-party state baseline for a joining client.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkJoinCampaignBaseline : IMessage
{
    [ProtoMember(1)]
    public readonly long ServerTicks;

    [ProtoMember(2)]
    public readonly MobilePartyJoinState[] PartyStates;

    [ProtoMember(3)]
    public readonly bool IsComplete;

    [ProtoMember(4)]
    public readonly TimeControlEnum TimeControlMode;

    public NetworkJoinCampaignBaseline(
        long serverTicks,
        TimeControlEnum timeControlMode,
        MobilePartyJoinState[] partyStates,
        bool isComplete = true)
    {
        ServerTicks = serverTicks;
        TimeControlMode = timeControlMode;
        PartyStates = partyStates;
        IsComplete = isComplete;
    }
}
