using Common.Messaging;

namespace GameInterface.Services.Save.Messages;

public readonly struct GameSaveStateChanged : IEvent
{
    public bool IsSaving { get; }

    public GameSaveStateChanged(bool isSaving)
    {
        IsSaving = isSaving;
    }
}
