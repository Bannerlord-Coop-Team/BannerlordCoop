using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services;
using GameInterface.Services.UI.CoopOptions.Providers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.UI.CoopOptions;

public interface ICoopOptionsVMFactory : IGameAbstraction
{
    CoopOptionsVM Create(Action close);
}

public sealed class CoopOptionsVMFactory : ICoopOptionsVMFactory
{
    private readonly ICoopOptionsStore optionsStore;
    private readonly IMessageBroker messageBroker;
    private readonly IReadOnlyList<ICoopOptionsTabProvider> tabProviders;

    public CoopOptionsVMFactory(
        ICoopOptionsStore optionsStore,
        IMessageBroker messageBroker,
        IEnumerable<ICoopOptionsTabProvider> tabProviders)
    {
        this.optionsStore = optionsStore;
        this.messageBroker = messageBroker;
        this.tabProviders = tabProviders.ToArray();
    }

    public CoopOptionsVM Create(Action close)
    {
        return new CoopOptionsVM(
            optionsStore,
            messageBroker,
            tabProviders,
            ModConfigProvider.ModOptions,
            close);
    }
}
