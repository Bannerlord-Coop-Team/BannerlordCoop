using Common.Messaging;
using System;

namespace GameInterface.Services.GameState.Messages;

public record LoadGameSave : ICommand
{
    public byte[] SaveData { get; }

    public LoadGameSave(byte[] saveData)
    {
        SaveData = saveData;
    }
}

public record GameSaveLoaded : IResponse
{
}
