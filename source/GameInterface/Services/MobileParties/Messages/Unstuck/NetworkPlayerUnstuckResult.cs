using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Unstuck;

/// <summary>
/// Server report of a <see cref="NetworkRequestPlayerUnstuck"/>: one line per exit it applied,
/// failed, or skipped. The requesting client uses its arrival to clear the local-only encounter and
/// menu state the server cannot see.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerUnstuckResult : IEvent
{
    [ProtoMember(1)]
    public string PartyId { get; }

    /// <summary>Protobuf round-trips an empty array as null; consumers must null-guard.</summary>
    [ProtoMember(2)]
    public string[] Actions { get; }

    public NetworkPlayerUnstuckResult(string partyId, string[] actions)
    {
        PartyId = partyId;
        Actions = actions;
    }
}
