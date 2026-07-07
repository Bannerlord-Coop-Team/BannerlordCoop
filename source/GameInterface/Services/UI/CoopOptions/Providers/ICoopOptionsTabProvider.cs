using Common.Messaging;
using GameInterface.Services.UI.CoopOptions;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers;

public interface ICoopOptionsTabProvider
{
    string Id { get; }

    CoopOptionsTabVM CreateTab(CoopOptionsData options, IMessageBroker messageBroker, Action<CoopOptionsTabVM> onSelect);
}
