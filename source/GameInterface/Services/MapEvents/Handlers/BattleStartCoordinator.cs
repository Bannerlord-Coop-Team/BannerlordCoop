using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Messages.Start;
using Serilog;
using System;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>The way a battle is being resolved, claimed per map event so the two modes stay mutually exclusive.</summary>
internal enum BattleStartMode
{
    Mission = 0,
    Simulation = 1,
    Unclaimed = 2,
}

/// <summary>
/// Client-side blocking gate for starting a battle. The attack / send-troops consequence prefixes call
/// <see cref="RequestBlocking"/>, which asks the server to start the battle in the given mode and blocks the game
/// thread (pumping) until the server accepts or rejects. The round-trip plumbing is the shared
/// <see cref="BlockingRequestGate{TReply}"/>; the server-side gate + setup lives behind
/// <see cref="BattleStartDispatcher"/>, which claims the mode and delegates to the per-mode starters, each of which
/// answers with <see cref="NetworkBattleStartReply"/>.
/// </summary>
internal class BattleStartCoordinator : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleStartCoordinator>();

    /// <summary>Statically reachable so the (static) consequence prefixes can reach the DI-wired coordinator.</summary>
    internal static BattleStartCoordinator Instance { get; private set; }

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly BlockingRequestGate<bool> gate = new BlockingRequestGate<bool>();

    public BattleStartCoordinator(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        Instance = this;

        messageBroker.Subscribe<NetworkBattleStartReply>(Handle_NetworkBattleStartReply);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleStartReply>(Handle_NetworkBattleStartReply);

        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// [Client] Blocks until the server accepts or rejects starting the battle in <paramref name="mode"/> for the
    /// given map event, then returns whether it was accepted. Blocking the game thread is safe here: the gate's
    /// WaitWhilePumping keeps draining the queue so the network thread (and the reply) still make progress.
    /// </summary>
    public bool RequestBlocking(BattleStartMode mode, string mapEventId, string attackerPartyId)
    {
        var pending = gate.Register();

        try
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

            // On a client, SendAll targets the server (its only connected peer).
            network.SendAll(new NetworkBattleStartRequest(pending.RequestId, (int)mode, mapEventId, attackerPartyId));

            if (!gate.WaitWhilePumping(pending, deadline))
            {
                Logger.Error("Timed out waiting for the server to accept the {Mode} battle start. MapEventId={MapEventId}", mode, mapEventId);
                return false;
            }

            return pending.Reply;
        }
        finally
        {
            gate.Release(pending);
        }
    }

    /// <summary>[Server -&gt; Client] A mode starter answered; complete the matching blocking request.</summary>
    private void Handle_NetworkBattleStartReply(MessagePayload<NetworkBattleStartReply> payload)
    {
        gate.Complete(payload.What.RequestId, payload.What.Accepted);
    }
}
