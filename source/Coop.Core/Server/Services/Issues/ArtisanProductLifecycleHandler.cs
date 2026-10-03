using Common.Messaging;
using Common;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Linq;

namespace Coop.Core.Server.Services.Issues;

internal sealed class ArtisanProductLifecycleHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IArtisanProductQuestActions actions;

    public ArtisanProductLifecycleHandler(IMessageBroker broker, IObjectManager objects,
        IPlayerManager players, IArtisanProductQuestActions actions)
    {
        this.broker = broker;
        this.objects = objects;
        this.players = players;
        this.actions = actions;
        broker.Subscribe<PlayerHeirSelectionCompleted>(HandleHeirSelected);
        broker.Subscribe<SavedPlayerRegistrationsRestored>(HandleRegistrationsRestored);
    }

    public void Dispose()
    {
        broker.Unsubscribe<PlayerHeirSelectionCompleted>(HandleHeirSelected);
        broker.Unsubscribe<SavedPlayerRegistrationsRestored>(HandleRegistrationsRestored);
    }

    private void HandleRegistrationsRestored(MessagePayload<SavedPlayerRegistrationsRestored> payload)
    {
        GameThread.RunSafe(() =>
        {
            // Vanilla checks this before saved players are registered and their authority can be resolved.
            actions.CancelInvalidLoadedIssues();
        });
    }

    private void HandleHeirSelected(MessagePayload<PlayerHeirSelectionCompleted> payload)
    {
        var hero = payload.What.PlayerHero;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetIdWithLogging(hero, out var heroId)) return;
            var player = players.Players.FirstOrDefault(candidate => candidate.HeroId == heroId);
            if (player == null) return;
            actions.CancelForPlayer(player.ControllerId);
        });
    }
}
