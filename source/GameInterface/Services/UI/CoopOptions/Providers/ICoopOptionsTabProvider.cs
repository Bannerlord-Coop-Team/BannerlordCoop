using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.UI.CoopOptions;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers;

public interface ICoopOptionsTabProvider
{
    string Id { get; }
    bool IsAvailable(ModOptions modOptions);

    CoopOptionsTabVM CreateTab(CoopOptionsData options, IMessageBroker messageBroker, Action<CoopOptionsTabVM> onSelect);
}
