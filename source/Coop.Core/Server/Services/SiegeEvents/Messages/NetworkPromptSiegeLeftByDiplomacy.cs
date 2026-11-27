using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// A diplomatic change pulled a player's party out of its own siege on the server. The owning client shows the
/// vanilla "no longer enemies" menu naming <see cref="FactionId"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPromptSiegeLeftByDiplomacy : IEvent
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string FactionId { get; }

    public NetworkPromptSiegeLeftByDiplomacy(string partyId, string factionId)
    {
        PartyId = partyId;
        FactionId = factionId;
    }
}
