using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Phase E (mid-battle join): a client that enters an ALREADY-ACTIVATED battle must be told the battle is live.
/// <c>NetworkBattleActivated</c> is a one-shot broadcast sent the moment the battle goes live, so a client that
/// joins afterward never hears it and would otherwise sit at <c>activated=false</c> — its NPC puppets stay frozen
/// through its own deployment (they are spawned un-paused, but the gate state is still wrong) and a later promotion
/// to host wouldn't release the NPCs it adopts (<c>OnPromotedToHost</c> gates on activation). The fix re-tells the
/// joiner over the mesh from <c>CoopBattleController.SendJoinInfo</c>.
/// <para>
/// Drives two clients over the mesh: the host activates BEFORE the joiner's controller exists (so the joiner
/// genuinely misses the broadcast — the real mid-battle-join ordering), then the join handshake catches it up.
/// </para>
/// </summary>
public class BattleActivationJoinTests : MissionTestEnvironment
{
    public BattleActivationJoinTests(ITestOutputHelper output) : base(output) { }

    // The gate state lives in the controller's private BattleDeploymentActivator; read it the same way the other
    // mirror tests reach controller internals (AccessTools), so we assert the propagated state, not a side effect.
    private static bool IsActivated(CoopBattleController controller)
    {
        var activator = (IBattleDeploymentActivator)AccessTools.Field(typeof(CoopBattleController), "_activator").GetValue(controller);
        return activator.IsActivated;
    }

    // What the server fans out to existing instance members when a new controller enters — it drives the host's
    // CoopMissionController.SendJoinInfo to the joiner over the mesh (join info + the activation catch-up).
    private static void TriggerJoinHandshake(EnvironmentInstance host, string joinerControllerId, string instanceId)
    {
        host.Call(() => host.Resolve<IMessageBroker>().Publish(host, new NetworkMissionPeerEntered(joinerControllerId, instanceId)));
    }

    [Fact]
    public void LateJoiner_IntoActivatedBattle_IsToldTheBattleIsLive()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "joiner");
        var host = Clients.First();
        var joiner = Clients.Skip(1).First();

        CoopBattleController hostController = null;
        host.Call(() =>
        {
            fixture.CreateMission(host);
            hostController = host.Resolve<CoopBattleController>();
        });

        EnterBattle(host, mapEventId);                          // elect "host" + set the controller's instance id
        host.Call(() => hostController.OnDeploymentFinished()); // host commits -> battle activated (+ a broadcast the joiner misses)
        Assert.True(IsActivated(hostController));

        // The joiner's controller comes alive only NOW — AFTER activation — so it never saw the broadcast.
        CoopBattleController joinerController = null;
        joiner.Call(() =>
        {
            fixture.CreateMission(joiner);
            joinerController = joiner.Resolve<CoopBattleController>();
            AccessTools.Field(typeof(CoopBattleController), "instanceId").SetValue(joinerController, mapEventId);
        });
        Assert.False(IsActivated(joinerController), "precondition: a late joiner misses the one-shot activation broadcast");

        // The join handshake catches it up: the activated host re-sends NetworkBattleActivated to the joiner.
        TriggerJoinHandshake(host, "joiner", mapEventId);

        Assert.True(IsActivated(joinerController), "the join handshake should tell a late joiner the battle is already live");

        GC.KeepAlive(hostController);
        GC.KeepAlive(joinerController);
    }

    [Fact]
    public void Joiner_IntoNotYetActivatedBattle_IsNotToldActive()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "joiner");
        var host = Clients.First();
        var joiner = Clients.Skip(1).First();

        CoopBattleController hostController = null;
        host.Call(() =>
        {
            fixture.CreateMission(host);
            hostController = host.Resolve<CoopBattleController>();
        });

        EnterBattle(host, mapEventId);                          // elected host, but deployment NOT committed -> not activated
        Assert.False(IsActivated(hostController));

        CoopBattleController joinerController = null;
        joiner.Call(() =>
        {
            fixture.CreateMission(joiner);
            joinerController = joiner.Resolve<CoopBattleController>();
            AccessTools.Field(typeof(CoopBattleController), "instanceId").SetValue(joinerController, mapEventId);
        });

        TriggerJoinHandshake(host, "joiner", mapEventId);

        Assert.False(IsActivated(joinerController), "a joiner into a not-yet-live battle must not be told it is active");

        GC.KeepAlive(hostController);
        GC.KeepAlive(joinerController);
    }
}
