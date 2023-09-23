using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages;

public record ResolveHero : ICommand
{
    public string PlayerId { get; }

    public ResolveHero(string playerId)
    {
        PlayerId = playerId;
    }
}