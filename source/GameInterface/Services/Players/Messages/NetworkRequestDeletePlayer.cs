using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Players.Messages;

/// <summary>
/// Client request to delete the requesting player: the server removes the player registration,
/// kills the hero, destroys the party, and disconnects the requesting client.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestDeletePlayer : ICommand
{
    /// <summary>The requesting client's own hero, for cross-checking and logging only; the server
    /// resolves the player to delete from the requesting connection, never from this id.</summary>
    [ProtoMember(1)]
    public string HeroId { get; }

    public NetworkRequestDeletePlayer(string heroId)
    {
        HeroId = heroId;
    }
}
