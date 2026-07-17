using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Services.MapEvents;
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

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-102 (Host Epoch and Stale Host Rejection): "Messages exercising host authority shall include the
/// sender's host epoch. Receivers shall reject host-authority messages bearing a stale epoch, so that
/// messages from a former host are rejected after authority has migrated to another player."
/// <para>
/// The most consequential host-authority message is the host's battle-conclusion report (a victory
/// <see cref="NetworkChangeBattleState"/>): the server commits the persistent campaign result from it. The
/// sender-identity gate (BR-011/BR-075) already refuses a report from a client that is not the CURRENT host —
/// but identity alone cannot refuse a report from the sender's EARLIER hosting stint once that same player is
/// host again (host A → migrated to B → migrated back to A). The epoch closes that hole: a report stamped
/// with the epoch of a previous hosting generation is refused even when the sender is the current host.
/// Mirrors <see cref="E2E.Tests.Services.MapEvents.HostAuthorityTests"/>' AI-vs-AI scaffolding so the
/// assertion stays on the authority gate and off the downstream reward machinery.
/// </para>
/// </summary>
public class HostEpochStaleConclusionTests : MapEventTestBase
{
    public HostEpochStaleConclusionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void StaleEpochVictoryReport_IsRefused_EvenFromTheCurrentHost()
    {
        // An AI-vs-AI battle registered on the server and replicated to both clients.
        var ctx = CreateServerMapEvent();

        // One peer-associated player: "host". Its assignment history is host -> other -> host again (two
        // migrations), so the CURRENT assignment carries epoch 3 while the sender's first stint was epoch 1.
        CreatePlayerHeroParty("host");
        var hostClient = Clients.First();
        Server.Resolve<IPlayerManager>().SetPeer("host", hostClient.NetPeer);
        Server.Call(() => Server.Resolve<IBattleHostRegistry>()
            .Set(ctx.MapEventId, new BattleHostAssignment("host", Array.Empty<string>(), epoch: 3)));

        var disabled = ResultCommitDisabledMethods();

        // Fires only when the server accepts a completion report and concludes the battle.
        int concluded = 0;
        Server.Resolve<IMessageBroker>().Subscribe<MapEventConcluded>(_ => concluded++);

        // A victory report from the sender's FIRST hosting stint (epoch 1) arrives after the migrations —
        // the in-flight "former host" message BR-102 exists to refuse. The sender IS the current host, so
        // the identity gate alone would let it through; the stale epoch must reject it.
        hostClient.Call(() => hostClient.Resolve<INetwork>()
            .SendAll(new NetworkChangeBattleState(ctx.MapEventId, BattleState.AttackerVictory, hostEpoch: 1)), disabled);

        // Refused: no conclusion was processed and the shared battle is untouched.
        Assert.Equal(0, concluded);
        AssertServerBattleState(ctx.MapEventId, BattleState.None);

        // The SAME report stamped with the CURRENT epoch is honored, proving the refusal above is the
        // stale-epoch gate and not a blanket rejection. This send goes through the PRODUCTION stamping
        // path: the client's own registry (seeded with the current assignment) supplies the epoch when its
        // MapEventHandler relays the battle-state change.
        hostClient.Call(() =>
        {
            hostClient.Resolve<IBattleHostRegistry>()
                .Set(ctx.MapEventId, new BattleHostAssignment("host", Array.Empty<string>(), epoch: 3));

            Assert.True(hostClient.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            hostClient.Resolve<IMessageBroker>().Publish(this,
                new MapEventBattleStateChangeAttempted(mapEvent, BattleState.AttackerVictory));
        }, disabled);

        Assert.Equal(1, concluded);
        Server.Call(() =>
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out _),
                $"MapEvent {ctx.MapEventId} should have been finalized/removed after the current-epoch report"));
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
    /// campaign, so disable them (the proven HostAuthorityTests boundary). <c>FinalizeEventAux</c> is
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
