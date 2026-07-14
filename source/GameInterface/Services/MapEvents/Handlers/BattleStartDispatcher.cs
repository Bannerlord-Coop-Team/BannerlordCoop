using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// [Server] The single entry point for a client's <see cref="NetworkBattleStartRequest"/>. Replaces the two former
/// subscribers that each self-filtered by mode. It resolves the map event and owns the one authoritative mode claim
/// (<see cref="ServerBattleModeArbiter"/>), then hands the request to the matching <see cref="IBattleModeStarter"/>,
/// which runs its mode-specific gates, decides how to act on the tri-state claim result, and owns its own replies.
/// </summary>
/// <remarks>
/// Resolve + gates + claim + the starter body all run inside one blocking <see cref="GameThread.RunSafe"/> for every
/// mode. That re-resolves a possibly-finalized event at drain time and keeps the claim from landing after a
/// concurrent finalize's <see cref="ServerBattleModeArbiter.Release"/> (which would leak a dangling arbiter entry),
/// while the single game-thread serialization also stands in for the network-thread ordering the simulation's
/// duplicate-session guard used to rely on. Adding a future mode is a new registered starter, not a third
/// self-filtering subscriber.
/// </remarks>
internal class BattleStartDispatcher : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleStartDispatcher>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IReadOnlyDictionary<int, IBattleModeStarter> startersByMode;

    public BattleStartDispatcher(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        BattleMissionStartHandler missionStarter,
        BattleSimulationRunHandler simulationStarter)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        var starters = new Dictionary<int, IBattleModeStarter>();
        Register(starters, missionStarter);
        Register(starters, simulationStarter);
        startersByMode = starters;

        messageBroker.Subscribe<NetworkBattleStartRequest>(Handle_NetworkBattleStartRequest);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleStartRequest>(Handle_NetworkBattleStartRequest);
    }

    private static void Register(IDictionary<int, IBattleModeStarter> starters, IBattleModeStarter starter)
        => starters[(int)starter.Mode] = starter;

    private void Handle_NetworkBattleStartRequest(MessagePayload<NetworkBattleStartRequest> payload)
    {
        if (ModInformation.IsClient)
            return;

        var request = payload.What;

        // Unknown / mis-cast mode: dropped, exactly as the two former per-mode filters silently returned.
        if (!startersByMode.TryGetValue(request.Mode, out var starter))
            return;

        var requester = payload.Who as NetPeer;

        // Resolve + gates + claim + starter body on the game thread (blocking): re-resolve at drain time (the event
        // may have finalized since the request arrived) and keep the claim atomic with resolution so it cannot land
        // after a concurrent finalize's Release.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObject(request.MapEventId, out MapEvent mapEvent))
                return;

            // The dispatcher owns the claim; the starter invokes it after its own pre-claim gates so mode-specific
            // rejections (e.g. mission's lords-hall) can leave the event unclaimed for auto-resolve.
            BattleClaimResult Claim() => starter.Mode == BattleStartMode.Mission
                ? ServerBattleModeArbiter.ClaimMission(request.MapEventId)
                : ServerBattleModeArbiter.ClaimSimulation(request.MapEventId);

            starter.HandleRequest(mapEvent, request, requester, Claim);
        }, blocking: true, context: nameof(Handle_NetworkBattleStartRequest));
    }
}
