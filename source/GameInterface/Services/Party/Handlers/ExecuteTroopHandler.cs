using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Party.Handlers;

internal class ExecuteTroopHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<ExecuteTroopHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ExecuteTroopHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<HeroExecuted>(Handle_HeroExecuted);
        messageBroker.Subscribe<ExecuteHero>(Handle_ExecuteHero);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeroExecuted>(Handle_HeroExecuted);
        messageBroker.Unsubscribe<ExecuteHero>(Handle_ExecuteHero);
    }

    private void Handle_HeroExecuted(MessagePayload<HeroExecuted> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.ExecutedHero, out var executedHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Executor, out var executorId)) return;

        var message = new ExecuteHero(executedHeroId, executorId);
        network.SendAll(message);
    }

    private void Handle_ExecuteHero(MessagePayload<ExecuteHero> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.ExecutedHeroId, out var executedHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.ExecutorId, out var executor)) return;

        KillCharacterAction.ApplyByExecution(executedHero, executor, true, false);
    }
}