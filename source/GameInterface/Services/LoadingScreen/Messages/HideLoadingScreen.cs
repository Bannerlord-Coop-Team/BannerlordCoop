using Common.Messaging;
using GameInterface.Services.LoadingScreen.Handlers;

namespace GameInterface.Services.LoadingScreen.Messages;

/// <summary>
/// HideLoadingScreen to publish with broker
/// </summary>
public class HideLoadingScreen : ICommand
{
    //The game's loading screen controls are too unstable, this is the only way I got it to work after trying many different approaches.
    public HideLoadingScreen()
    {
        var loadingScreenHandler = new CoopLoadingScreenHandler(MessageBroker.Instance);
    }
}
