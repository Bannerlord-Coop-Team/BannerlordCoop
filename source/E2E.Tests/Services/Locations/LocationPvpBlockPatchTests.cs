using System.Reflection;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Locations;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>
/// PVP is disabled in coop location missions: <c>LocationPvpBlockPatch</c> (an <c>Agent.RegisterBlow</c>
/// prefix) suppresses any blow whose victim or attacker is a remote player's puppet (human,
/// <see cref="AgentControllerType.None"/>) — locations have no damage routing, so an applied blow would be
/// local-only divergence. The prefix is invoked directly (reflection — the patch class is internal) against
/// mock-engine agents so the verdicts are deterministic and independent of prefix ordering with the
/// mock-engine's own RegisterBlow shim.
/// </summary>
public class LocationPvpBlockPatchTests : E2ETestEnvironment
{
    public LocationPvpBlockPatchTests(ITestOutputHelper output) : base(output) { }

    private static readonly MethodInfo PrefixMethod = AccessTools.Method(
        typeof(ILocationMissionBehavior).Assembly
            .GetType("GameInterface.Services.Locations.Patches.LocationPvpBlockPatch"),
        "Prefix");

    /// <summary>Runs the patch prefix; the return value is Harmony's "run the original" verdict.</summary>
    private static bool BlowApplies(Agent victim, Blow blow)
        => (bool)PrefixMethod.Invoke(null, new object[] { victim, blow });

    [Fact]
    public void LocationMission_BlowsToAndFromPuppets_AreSuppressed()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            Assert.True(LocationMissionTracker.TryRegister(mock.Shell));

            var character = Game.Current.PlayerTroop;
            var puppet = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            var npc = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            var player = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.Player));

            // The local player hits a remote player's puppet -> suppressed.
            Assert.False(BlowApplies(puppet, new Blow(player.Index) { InflictedDamage = 10 }));

            // A puppet's replicated swing hits the local player -> suppressed.
            Assert.False(BlowApplies(player, new Blow(puppet.Index) { InflictedDamage = 10 }));

            // The puppet's mount counts as the puppet: the horse can't be killed from under them.
            var horse = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            horseMirror.IsHuman = false;
            horseMirror.IsMount = true;
            horseMirror.RiderAgent = puppet;
            Assert.False(BlowApplies(horse, new Blow(player.Index) { InflictedDamage = 10 }));

            // Local player vs a native NPC (both locally controlled) still lands — only PVP is blocked.
            Assert.True(BlowApplies(npc, new Blow(player.Index) { InflictedDamage = 10 }));
        });
    }

    [Fact]
    public void NonLocationMission_PuppetBlows_AreUntouched()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();

        client.Call(() =>
        {
            // NOT registered with LocationMissionTracker — e.g. a coop field battle, where
            // BattleBlowInterceptPatch owns puppet blows and this patch must stay out of the way.
            var mock = fixture.CreateMission(client);

            var character = Game.Current.PlayerTroop;
            var puppet = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            var player = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.Player));

            Assert.True(BlowApplies(puppet, new Blow(player.Index) { InflictedDamage = 10 }));
        });
    }

    [Fact]
    public void LocationPvpBlockPatch_IsDiscoverableByPatchAll_AndHooksRegisterBlow()
    {
        var patchType = typeof(ILocationMissionBehavior).Assembly
            .GetType("GameInterface.Services.Locations.Patches.LocationPvpBlockPatch");
        Assert.NotNull(patchType);

        var harmony = new Harmony("e2e.locationpvp.patchtest");
        try
        {
            // CreateClassProcessor(...).Patch() is exactly what PatchAll runs per type.
            var patched = harmony.CreateClassProcessor(patchType).Patch()
                          ?? new List<MethodInfo>();

            Assert.Contains(patched, m => m.Name.Contains(nameof(Agent.RegisterBlow)));
        }
        finally
        {
            harmony.UnpatchAll("e2e.locationpvp.patchtest");
        }
    }
}
