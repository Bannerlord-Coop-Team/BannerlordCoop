using Common.Logging;
using Common.Messaging;
using ProtoBuf;
using Serilog;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Message from Client to Server for validating the client
/// Responsibilities
/// 1. Associate client with existing hero
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkClientValidate : ICommand
{
    private static readonly ILogger Logger = LogManager.GetLogger<NetworkClientValidate>();

    [ProtoMember(1)]
    public string PlayerId { get; }

    public NetworkClientValidate(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            Logger.Error("Controller Id was not set properly before validation has started");
        }

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
