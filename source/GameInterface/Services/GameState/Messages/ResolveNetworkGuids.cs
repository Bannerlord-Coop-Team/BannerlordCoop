using Common.Messaging;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct ResolveNetworkGuids : ICommand
    {
    }

    public readonly struct NetworkGuidsResolved : ICommand
    {
    }
}
