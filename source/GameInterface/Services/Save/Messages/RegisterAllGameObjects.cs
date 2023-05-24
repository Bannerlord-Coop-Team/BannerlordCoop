using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages;

public record RegisterAllGameObjects : ICommand
{
}

public record AllGameObjectsRegistered : IResponse
{
}