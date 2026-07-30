using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// Clears a malformed besieger-camp link that could not use the normal replicated property setter.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkClearStaleBesiegerCamp : IEvent
{
    [ProtoMember(1)]
    public string PartyId { get; }

    public NetworkClearStaleBesiegerCamp(string partyId)
    {
        PartyId = partyId;
    }
}
