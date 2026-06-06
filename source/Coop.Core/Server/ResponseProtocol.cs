using Common.Logging;
using Common.Messaging;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server;

/// <summary>
/// Protocol for sending a trigger message to all clients and waiting for a response from all clients.
/// </summary>
/// <remarks>
/// Timeout is supported.
/// </remarks>
/// <typeparam name="ResponseType">Expected response message type to wait for</typeparam>
internal class ResponseProtocol<ResponseType> where ResponseType : IMessage
{
    private readonly ICoopServer server;
    private readonly IMessageBroker messageBroker;
    private readonly Task[] responseTasks;

    public ResponseProtocol(
        ICoopServer server,
        IMessageBroker messageBroker,
        TimeSpan timeout)
    {
        this.server = server;
        this.messageBroker = messageBroker;

        responseTasks = server.ConnectedPeers.Select(peer =>
        {
            var responseTask = new PeerResponse<ResponseType>(messageBroker, peer, timeout);

            return responseTask.Task;
        }).ToArray();
    }

    /// <summary>
    /// Sends a trigger message to all clients, waits for a response from all clients (or timeout), and then sends a single notify completion message internally.
    /// </summary>
    /// <typeparam name="TriggerType">Message type to send to all clients</typeparam>
    /// <typeparam name="NotifyType">Message type to send internally after all clients have responeded (or timeout)</typeparam>
    /// <param name="triggerMessage">Message to send to all clients</param>
    /// <param name="notifyMessage">Message to send internally after all clients have responeded (or timeout)</param>
    public Task StartResponseProtocol<TriggerType, NotifyType>(TriggerType triggerMessage, NotifyType notifyMessage)
        where TriggerType : IMessage
        where NotifyType : IMessage
    {
        // Send trigger message -> clients respond with response message -> notify message is sent internally

        // Wait for responses from all clients (cancles after timeout)
        var task = Task.WhenAll(responseTasks).ContinueWith(_ =>
        {
            messageBroker.Publish(this, notifyMessage);
        });

        // Send message to trigger 
        server.SendAll(triggerMessage);

        return task;
    }
}

/// <summary>
/// Helper class for waiting on a specific peer to respond with a message.
/// Timeout is supported.
/// </summary>
/// <typeparam name="T">Message to wait for</typeparam>
class PeerResponse<T> : IDisposable where T : IMessage
{
    private readonly ILogger Logger = LogManager.GetLogger<PeerResponse<T>>();

    public Task Task { get; private set; }

    private readonly IMessageBroker messageBroker;
    private readonly NetPeer peer;
    private readonly TaskCompletionSource<bool> tcs;

    public PeerResponse(IMessageBroker messageBroker, NetPeer peer, TimeSpan timeout)
    {
        this.peer = peer;
        this.messageBroker = messageBroker;

        var cts = new CancellationTokenSource(timeout);
        tcs = new TaskCompletionSource<bool>();
        messageBroker.Subscribe<T>(HandleMessage);

        cts.Token.Register(() =>
        {
            if (tcs.TrySetCanceled())
            {
                Logger.Error("Timeout waiting for peer {peer} to create hero", peer);
            }
        });

        Task = tcs.Task;
        Task.ContinueWith(_ => Dispose());
    }

    private void HandleMessage(MessagePayload<T> payload)
    {
        if (payload.Who as NetPeer == peer)
        {
            tcs.TrySetResult(true);
        }
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<T>(HandleMessage);
    }
}
