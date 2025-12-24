using Common.Messaging;
using GameInterface.Services.Save.Data;
using System;

namespace GameInterface.Services.Save.Messages;

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
