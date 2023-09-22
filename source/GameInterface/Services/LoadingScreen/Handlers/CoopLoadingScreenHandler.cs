using System.Collections.Generic;
using Common.Messaging;
using GameInterface.Services.LoadingScreen.Interfaces;
using GameInterface.Services.LoadingScreen.Messages;
using TaleWorlds.Library;

namespace GameInterface.Services.LoadingScreen.Handlers;

/// <summary>
/// Handler for the CoopLoadingScreen
/// </summary>
/// <remarks>
/// To show the loading screen, just use 
/// MessageBroker.Instance.Publish(this, new ShowLoadingScreen()); 
/// To hide it, use
/// MessageBroker.Instance.Publish(this, new HideLoadingScreen());
/// </remarks>
public class CoopLoadingScreenHandler : IHandler
{
    private readonly CoopLoadingScreen loadingScreen = CoopLoadingScreen.Instance;
    private readonly IMessageBroker messageBroker;

    public CoopLoadingScreenHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<ShowLoadingScreen>(Handle);
        messageBroker.Subscribe<HideLoadingScreen>(Handle);
    }

    private void Handle(MessagePayload<ShowLoadingScreen> payload)
    {
        loadingScreen.Initialize();
        loadingScreen.EnableLoadingWindow();
    }

    private void Handle(MessagePayload<HideLoadingScreen> payload)
    {
        loadingScreen.DisableLoadingWindow();
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ShowLoadingScreen>(Handle);
        messageBroker.Unsubscribe<HideLoadingScreen>(Handle);
    }

    /*The part below is to test the show and hide mechanics in console, can be used as follows in console:
     * tutorial.testShow
     * tutorial.testHide                                                                       */

    [CommandLineFunctionality.CommandLineArgumentFunction("testShow", "tutorial")]
    public static string testShow(List<string> strings)
    {
        MessageBroker.Instance.Publish(null, new ShowLoadingScreen());
        return "Command Executed!";
    }

    [CommandLineFunctionality.CommandLineArgumentFunction("testHide", "tutorial")]
    public static string testHide(List<string> strings)
    {
        MessageBroker.Instance.Publish(null, new HideLoadingScreen());
        return "Command Executed!";
    }
}
