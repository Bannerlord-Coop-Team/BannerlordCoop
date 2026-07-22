using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// BR-011 (Host Authority): "The mission host shall be authoritative for the battle mission state delegated to
/// the mission host by the server. The campaign server shall remain authoritative for persistent campaign state
/// and final battle results."
///
/// The concrete authority boundary the server enforces is the battle result commit: a victory
/// <see cref="BattleState"/> is a persistent campaign result, so only the elected mission host may drive it. A
/// non-host client's attempt to commit a victory must be refused by the server rather than independently applied
/// (mirrors the <c>MapEventResultCommit</c> pattern, but for the server-side host-gate in
/// <c>MapEventHandler.Handle_NetworkChangeBattleState</c>). To keep the assertion on the authority gate itself and
/// off the downstream reward machinery, the battle is AI-vs-AI (no player parties on either side, so no capture
/// and no per-player encounter close). The refused report leaves the event alive and unconcluded
/// (<see cref="BattleState.None"/>); the honored report concludes it (<c>MapEventConcluded</c> + the event torn
/// down by the server's finalize). <c>FinalizeEventAux</c> itself must NOT be disable-patched: it already carries
/// a production Harmony prefix, and a second prefix is the known InvalidProgramException trap.
/// </summary>
public class HostAuthorityTests : MapEventTestBase
{
    public HostAuthorityTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Requirement", "BR-011")]
    public void NonHostCommitOfVictoryBattleState_IsRefused_WhileTheHostsIsApplied()
    {
        // An AI-vs-AI battle registered on the server and replicated to both clients.
        var ctx = CreateServerMapEvent();

        // Two peer-associated players: "host" (elected battle host) and "guest" (a non-host). Neither party is a
        // participant in the AI-vs-AI event — they exist only so the server can resolve the sending peer to a
        // controller id and compare it against the host assignment (the authority gate under test).
        CreatePlayerHeroParty("host");
        CreatePlayerHeroParty("guest");

        var hostClient = Clients.First();
        var guestClient = Clients.Last();

        // Associate each client's connection with its controller on the SERVER's player registry so
        // Handle_NetworkChangeBattleState can resolve the sender, and seed the server's host assignment.
        Server.Resolve<IPlayerManager>().SetPeer("host", hostClient.NetPeer);
        Server.Resolve<IPlayerManager>().SetPeer("guest", guestClient.NetPeer);
        Server.Call(() => Server.Resolve<IBattleHostRegistry>()
            .Set(ctx.MapEventId, new BattleHostAssignment("host", Array.Empty<string>())));

        var disabled = ResultCommitDisabledMethods();

        // Fires only when the server accepts a completion report and concludes the battle.
        bool concluded = false;
        Server.Resolve<IMessageBroker>().Subscribe<MapEventConcluded>(_ => concluded = true);

        // The non-host guest commits a victory. Its connection is the sender the server resolves.
        guestClient.Call(() => guestClient.Resolve<INetwork>()
            .SendAll(new NetworkChangeBattleState(ctx.MapEventId, BattleState.AttackerVictory)), disabled);

        // Refused: the server left the shared battle unconcluded — the final result is the server's authority
        // delegated only to the elected host, not something a non-host can commit.
        Assert.False(concluded);
        AssertServerBattleState(ctx.MapEventId, BattleState.None);

        // The SAME message from the elected host IS applied, proving the refusal is a host-authority gate and not
        // a blanket rejection of the message: the server concludes the battle and finalizes (tears down) the event.
        hostClient.Call(() => hostClient.Resolve<INetwork>()
            .SendAll(new NetworkChangeBattleState(ctx.MapEventId, BattleState.AttackerVictory)), disabled);

        Assert.True(concluded);
        Server.Call(() =>
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out _),
                $"MapEvent {ctx.MapEventId} should have been finalized/removed by the server after the host's report"));
    }

    private void AssertServerBattleState(string mapEventId, BattleState expected)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent),
                $"MapEvent {mapEventId} should still exist on the server");
            Assert.Equal(expected, mapEvent.BattleState);
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// The world-dependent result/capture/loot steps a victory <see cref="BattleState"/> drives need a live
    /// campaign, so disable them (the proven BattleConcludesWithVictory boundary). <c>FinalizeEventAux</c> is
    /// deliberately NOT disabled — it already carries a production Harmony prefix, and stacking the disable
    /// prefix on it is the known two-prefix InvalidProgramException trap — so the honored report is observed
    /// via <c>MapEventConcluded</c> + the event's removal instead of a post-finalize state read.
    /// </summary>
    private IReadOnlyList<MethodBase> ResultCommitDisabledMethods()
        => MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CaptureDefeatedPartyMembers"))
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .Append(AccessTools.Method(typeof(MapEventRegistry), "CloseDestroyedMapEventEncounterIfNeeded"))
            .ToList();
}
