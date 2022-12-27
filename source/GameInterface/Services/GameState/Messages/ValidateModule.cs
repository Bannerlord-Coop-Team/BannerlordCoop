using Common.Messaging;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct ValidateModule : ICommand
    {
        
    }

    public readonly struct ModulesValidated : IEvent
    {
    }
}
