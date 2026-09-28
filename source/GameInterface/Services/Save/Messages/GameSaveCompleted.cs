using Common.Messaging;

namespace GameInterface.Services.Save.Messages;

/// <summary>Reports the result of a save run by the authoritative game's SaveHandler.</summary>
public readonly struct GameSaveCompleted : IEvent
{
    public string SaveName { get; }
    public bool Success { get; }

    public GameSaveCompleted(string saveName, bool success)
    {
        SaveName = saveName;
        Success = success;
    }
}
