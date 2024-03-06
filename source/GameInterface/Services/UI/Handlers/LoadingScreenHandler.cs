using Common;
using Common.Messaging;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.Messages;
using GameInterface.Services.UI.Patches;
using TaleWorlds.Engine;

namespace GameInterface.Services.UI.Handlers;

internal class LoadingScreenHandler : IHandler
{
    private readonly IUIInterface UIInterface;
    private readonly IMessageBroker messageBroker;

    public LoadingScreenHandler(IUIInterface UIInterface, IMessageBroker messageBroker)
    {
        this.UIInterface = UIInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<StartLoadingScreen>(Handle);
        messageBroker.Subscribe<EndLoadingScreen>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<StartLoadingScreen>(Handle);
        messageBroker.Unsubscribe<EndLoadingScreen>(Handle);
    }

    private void Handle(MessagePayload<StartLoadingScreen> obj)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            LoadingWindow.EnableGlobalLoadingWindow();
        });
    }

    private void Handle(MessagePayload<EndLoadingScreen> obj)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            ShowMenuUntilLoadedPatch.IsDoneLoading = true;
            LoadingWindow.DisableGlobalLoadingWindow();
        });
    }
}
