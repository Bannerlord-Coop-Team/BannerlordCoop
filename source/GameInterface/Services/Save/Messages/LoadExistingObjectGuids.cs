using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using System;

namespace GameInterface.Services.Heroes.Messages;

public record LoadExistingObjectGuids : ICommand
{
    public GameObjectGuids GameObjectGuids { get; }

    public LoadExistingObjectGuids(
        GameObjectGuids gameObjectGuids)
    {
        GameObjectGuids = gameObjectGuids;
    }
}

public record ExistingObjectGuidsLoaded : IResponse
{
}
