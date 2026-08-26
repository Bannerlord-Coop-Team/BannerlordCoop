using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab;
using GameInterface.Services.UI.CoopOptions.Providers.NetworkTab;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using System;

namespace GameInterface.Tests.Services.UI;

internal static class CoopOptionsVMTestFactory
{
    public static CoopOptionsVM Create(
        ICoopOptionsStore optionsStore,
        IMessageBroker messageBroker,
        Action close = null)
    {
        ICoopOptionsTabProvider[] providers =
        {
            new KillFeedOptionsTabProvider(),
            new MapTimeOptionsTabProvider(),
            new ChatOptionsTabProvider(),
            new PlayerNameplatesOptionsTabProvider(),
            new NetworkOptionsTabProvider(),
        };
        return new CoopOptionsVM(
            optionsStore,
            messageBroker,
            providers,
            new ModOptions(new ModOptionsData()),
            close ?? (() => { }));
    }
}
