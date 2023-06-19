using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Message from Client to Server for validating the client
/// Responsibilities
/// 1. Associate client with existing hero
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkClientValidate : ICommand
{
    [ProtoMember(1)]
    public string PlayerId { get; }

    public NetworkClientValidate(string playerId)
    {
        PlayerId = playerId;
    }
}

/// <summary>
/// Response to <see cref="NetworkClientValidate"/> when successful
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkClientValidated : IResponse
{
    [ProtoMember(1)]
    public bool HeroExists { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkClientValidated(bool heroExists, string heroId)
    {
        HeroExists = heroExists;
        HeroId = heroId;
    }
}
