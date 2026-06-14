using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using System.Threading;

namespace Coop.Core.Server.Services.Connection.Handlers;

/// <summary>
/// Sends player-facing messaging about the loading lock. It tracks the loading count from the
/// registry's <see cref="LoadingPlayersChanged"/> signal so it never has to query connection
/// state itself.
/// </summary>
public class PlayerConnectionServerMessageHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    private const string UnpauseReadyMessage = "All players connected, game can now be un-paused";

    private int loadingPlayerCount;

    public PlayerConnectionServerMessageHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<LoadingPlayersChanged>(Handle_LoadingPlayersChanged);
        messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChangeAttempted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LoadingPlayersChanged>(Handle_LoadingPlayersChanged);
        messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChangeAttempted);
    }

    internal void Handle_LoadingPlayersChanged(MessagePayload<LoadingPlayersChanged> obj)
    {
        Volatile.Write(ref loadingPlayerCount, obj.What.LoadingPlayerCount);

        BroadcastNotification(loadingPlayerCount > 0 ? LoadingMessage(loadingPlayerCount) : UnpauseReadyMessage);
    }

    internal void Handle_TimeSpeedChangeAttempted(MessagePayload<TimeSpeedChangedAttempted> obj)
    {
        // Remind whoever just tried to change the speed why it is locked.
        if (Volatile.Read(ref loadingPlayerCount) <= 0) return;

        BroadcastNotification(LoadingMessage(loadingPlayerCount));
    }

    private void BroadcastNotification(string text)
    {
        var message = new SendInformationMessage(text);
        messageBroker.Publish(this, message);
        network.SendAll(message);
    }

    private static string LoadingMessage(int loadingPlayers) =>
        "Time controls disabled, " + loadingPlayers + " player(s) are currently joining the game";
}
