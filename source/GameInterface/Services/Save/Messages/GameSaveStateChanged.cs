using Common.Messaging;

namespace GameInterface.Services.Save.Messages;

/// <summary>Reports a change in the authoritative game's save state.</summary>
public readonly struct GameSaveStateChanged : IEvent
{
    public bool IsSaving { get; }

    public GameSaveStateChanged(bool isSaving)
    {
        IsSaving = isSaving;
    }
}
