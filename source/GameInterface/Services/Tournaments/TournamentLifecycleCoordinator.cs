using Common.Messaging;
using GameInterface.Services.Tournaments.Messages;

namespace GameInterface.Services.Tournaments;

public interface ITournamentLifecycleCoordinator : IGameAbstraction
{
    bool CompleteActiveSessionsAndReleasePreparation();
}

internal sealed class TournamentLifecycleCoordinator : ITournamentLifecycleCoordinator
{
    private readonly IMessageBroker messageBroker;
    private readonly ITournamentSessionRegistry sessionRegistry;

    public TournamentLifecycleCoordinator(
        IMessageBroker messageBroker,
        ITournamentSessionRegistry sessionRegistry)
    {
        this.messageBroker = messageBroker;
        this.sessionRegistry = sessionRegistry;
    }

    public bool CompleteActiveSessionsAndReleasePreparation()
    {
        messageBroker.Publish(this, new TournamentOrderlyShutdownRequested());
        return sessionRegistry.GetAll().Length == 0;
    }
}
