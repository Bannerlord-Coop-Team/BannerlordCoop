using Common;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Messages;
using GameInterface.Services.UI.Patches;

namespace GameInterface.Services.UI.Handlers;

internal class LoadingScreenHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public LoadingScreenHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<StartLoadingScreen>(Handle);
        messageBroker.Subscribe<EndLoadingScreen>(Handle);
        messageBroker.Subscribe<CampaignReady>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<StartLoadingScreen>(Handle);
        messageBroker.Unsubscribe<EndLoadingScreen>(Handle);
        messageBroker.Unsubscribe<CampaignReady>(Handle);
    }

    private void Handle(MessagePayload<StartLoadingScreen> obj)
    {
        GameLoopRunner.RunOnMainThread(LoadingWindowPatches.Begin);
    }

    private void Handle(MessagePayload<EndLoadingScreen> obj)
    {
        GameLoopRunner.RunOnMainThread(LoadingWindowPatches.End);
    }

    private void Handle(MessagePayload<CampaignReady> obj)
    {
        // The world has finished loading and the map is now visible; release the forced
        // loading window. Only acts when we were forcing it, so normal loads are untouched.
        if (LoadingWindowPatches.ForceLoadingWindow == false) return;

        GameLoopRunner.RunOnMainThread(LoadingWindowPatches.End);
    }
}
