using Common.Messaging;
using GameInterface.Services.LoadingScreen.Handlers;

namespace GameInterface.Services.LoadingScreen.Messages;

/// <summary>
/// Hides the loading screen if shown
/// </summary>
public record HideLoadingScreen : ICommand
{
}
