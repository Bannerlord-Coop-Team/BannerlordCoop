using Common.Messaging;
using GameInterface.Services.LoadingScreen.Handlers;

namespace GameInterface.Services.LoadingScreen.Messages;

/// <summary>
/// ShowLoadingScreen to publish with broker
/// </summary>
public class ShowLoadingScreen : ICommand
{
    //The game's loading screen controls are too unstable, this is the only way I got it to work after trying many different approaches.
    public ShowLoadingScreen()
    {
        var loadingScreenHandler = new CoopLoadingScreenHandler(MessageBroker.Instance);
    }
}
