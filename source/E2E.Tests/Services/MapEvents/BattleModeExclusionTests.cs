using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// End-to-end coverage of the mutual exclusion between a map event's two resolution modes — a playable
/// battle mission and a player-selected auto-resolve simulation (BR-001, BR-003). Once one mode has claimed
/// an event on the server (<see cref="ServerBattleModeArbiter"/>), the opposing mode's
/// <see cref="NetworkBattleStartRequest"/> is refused end-to-end: the mode handler replies
/// <see cref="NetworkBattleStartReply"/> with <c>Accepted == false</c> and opens nothing. The two
/// directions here close the "mission-held blocks simulation" gap that the arbiter unit tests only cover
/// implicitly, and drive the handler-level rejection the existing suite never exercised. The claiming mode is
/// established through the authoritative arbiter (the exact server-side state a successful start produces),
/// then the conflicting request is driven through the real handler over the mock campaign network.
/// </summary>
public class BattleModeExclusionTests : MapEventTestBase
{
    public BattleModeExclusionTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// BR-003 para 1 / BR-001: player simulation ("Send Troops") must not be available for a map event after a
    /// playable battle mission has been created for it. With the mission mode already claimed, a client's
    /// simulation-mode <see cref="NetworkBattleStartRequest"/> is rejected at the handler and no simulation is
    /// opened.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-001")]
    [Trait("Requirement", "BR-003")]
    public void MissionClaimedForEvent_PlayerSimulationStartRequest_IsRejectedEndToEnd()
    {
        var ctx = CreateServerMapEvent();
        var client = Clients.First();

        try
        {
            // A playable battle mission has been created for this map event (the mission handler's claim).
            Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(ctx.MapEventId)));
            Server.NetworkSentMessages.Clear();

            // A second player selects "Send Troops" (auto-resolve) for the SAME event.
            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Simulation,
                ctx.MapEventId,
                ctx.AttackerPartyId)), MapEventDisabledMethods);

            // The auto-resolve is refused end-to-end: rejected reply, no spectator window opened, no mode claimed.
            var reply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>());
            Assert.False(reply.Accepted);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkOpenBattleSimulation>());
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>());

            // The mission still owns the event afterwards — the failed simulation claim did not disturb it.
            Server.Call(() => Assert.False(ServerBattleModeArbiter.TryClaimSimulation(ctx.MapEventId)));
        }
        finally
        {
            Server.Call(() => ServerBattleModeArbiter.Release(ctx.MapEventId));
        }
    }

    /// <summary>
    /// BR-003 para 2 / BR-001: a playable battle mission must not be created for a map event after player
    /// simulation of it has begun. With the simulation mode already claimed, a client's mission-mode
    /// <see cref="NetworkBattleStartRequest"/> is rejected at the handler and no attack mission is broadcast.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-001")]
    [Trait("Requirement", "BR-003")]
    public void SimulationClaimedForEvent_MissionStartRequest_IsRejectedEndToEnd()
    {
        var ctx = CreateServerMapEvent();
        var client = Clients.First();

        try
        {
            // Player simulation of this map event has begun (the simulation handler's claim).
            Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimSimulation(ctx.MapEventId)));
            Server.NetworkSentMessages.Clear();

            // A player now presses "Attack" (open the live mission) for the SAME event.
            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                ctx.MapEventId,
                ctx.AttackerPartyId)), MapEventDisabledMethods);

            // The mission start is refused end-to-end: rejected reply, no mission broadcast, no mode claimed.
            var reply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>());
            Assert.False(reply.Accepted);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>());
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>());

            // The simulation still owns the event afterwards — the failed mission claim did not disturb it.
            Server.Call(() => Assert.False(ServerBattleModeArbiter.TryClaimMission(ctx.MapEventId)));
        }
        finally
        {
            Server.Call(() => ServerBattleModeArbiter.Release(ctx.MapEventId));
        }
    }
}
