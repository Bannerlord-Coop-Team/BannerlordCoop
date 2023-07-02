using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using System;

namespace GameInterface.Services.Heroes.Messages;

public record PackageObjectGuids : ICommand
{
}

public record ObjectGuidsPackaged : IResponse
{
    public string UniqueGameId { get; }
    public GameObjectGuids GameObjectGuids { get; }

    public ObjectGuidsPackaged(
        string uniqueGameId,
        GameObjectGuids gameObjectGuids)
    {
        UniqueGameId = uniqueGameId;
        GameObjectGuids = gameObjectGuids;
    }
}
