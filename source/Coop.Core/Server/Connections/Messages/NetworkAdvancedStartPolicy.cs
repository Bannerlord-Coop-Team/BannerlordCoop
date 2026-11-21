using Common.Messaging;
using GameInterface.Services.AdvancedStart.Data;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Client to server when character creation starts: which starting options the new character can pick. The server
/// replies once, then pushes a new policy whenever it changes until the hero joins.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestAdvancedStartPolicy : ICommand
{
}

/// <summary>
/// The server's policy, in reply to <see cref="NetworkRequestAdvancedStartPolicy"/> or pushed when it changes.
/// A null policy means the host turned the options off.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkAdvancedStartPolicy : IEvent
{
    [ProtoMember(1)]
    public AdvancedStartPolicy Policy { get; }

    public NetworkAdvancedStartPolicy(AdvancedStartPolicy policy)
    {
        Policy = policy;
    }
}
