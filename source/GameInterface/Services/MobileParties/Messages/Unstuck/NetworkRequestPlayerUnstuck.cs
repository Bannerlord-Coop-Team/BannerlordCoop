using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Unstuck;

/// <summary>
/// Client request for a server-authoritative unstuck of the given player party. Deliberately its own
/// message with its own handler: recovery must keep working when one of the normal exit request
/// flows is exactly what is wedged.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestPlayerUnstuck : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }

    /// <summary>Requesting player's hero, when the client could resolve one; the server falls back
    /// to the party leader and the player registry.</summary>
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkRequestPlayerUnstuck(string partyId, string heroId)
    {
        PartyId = partyId;
        HeroId = heroId;
    }
}
