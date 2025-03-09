using Common.Messaging;
using System;

namespace GameInterface.Registry.Messages;

public record RegisterAllGameObjects : ICommand
{
}

public record AllGameObjectsRegistered : IEvent
{
}