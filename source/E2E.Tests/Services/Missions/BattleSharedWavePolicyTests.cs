using System;
using System.Collections.Generic;
using System.Linq;
using Common.Util;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Missions.Battles;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// #2927: every owner sizes its native spawn phases from the same server-authored battle size and side totals,
/// so the reinforcement policy must not read a client-local option either. Bannerlord's "Reinforcement Waves"
/// option (3, 4, 5 or unlimited) caps a phase's lifetime spawn count, so a client set to 3 waves stranded its
/// owned reserves (ShoT_UP: 671 owned, 200 initial, three 100-troop waves, 171 never fielded) while another
/// owner in the same battle fielded everything. These tests run the handler's real joint Init through the
/// native <see cref="DefaultBattleMissionAgentSpawnLogic.Init"/> with each local option.
/// </summary>
public class BattleSharedWavePolicyTests : MissionTestEnvironment
{
    private const int UnlimitedWaveOptionIndex = 3;
    private const int DefenderSideTotal = 654;
    private const int AttackerOwned = 671;
    private const int ServerBattleSize = 400;

    public BattleSharedWavePolicyTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(0)] // 3 waves
    [InlineData(1)] // 4 waves
    [InlineData(2)] // 5 waves
    [InlineData(UnlimitedWaveOptionIndex)]
    public void LocalReinforcementWaveOption_DoesNotStrandOwnedReserves(int localWaveOptionIndex)
    {
        var phases = RunJointInitWithLocalWaveOption(localWaveOptionIndex);

        // The attacker owner fields its whole reserve over the battle: its initial share now, the rest as waves.
        var attacker = phases[(int)BattleSideEnum.Attacker][0];
        Assert.Equal(AttackerOwned, attacker.TotalSpawnNumber);
        Assert.Equal(attacker.TotalSpawnNumber, attacker.InitialSpawnNumber + attacker.RemainingSpawnNumber);
        Assert.InRange(attacker.InitialSpawnNumber, 1, ServerBattleSize);
    }

    [Fact]
    public void OwnersWithDifferentLocalWaveOptions_ComputeTheSameQuotas()
    {
        var threeWaves = RunJointInitWithLocalWaveOption(0);
        var unlimited = RunJointInitWithLocalWaveOption(UnlimitedWaveOptionIndex);

        foreach (var side in new[] { BattleSideEnum.Defender, BattleSideEnum.Attacker })
        {
            var a = threeWaves[(int)side][0];
            var b = unlimited[(int)side][0];
            Assert.Equal(b.TotalSpawnNumber, a.TotalSpawnNumber);
            Assert.Equal(b.InitialSpawnNumber, a.InitialSpawnNumber);
            Assert.Equal(b.RemainingSpawnNumber, a.RemainingSpawnNumber);
        }
    }

    private List<MissionSpawnPhase>[] RunJointInitWithLocalWaveOption(int localWaveOptionIndex)
    {
        List<MissionSpawnPhase>[] phases = null;
        var client = Clients.First();
        client.Call(() =>
        {
            using var fixture = new MissionEngineFixture();
            using var engine = new HeadlessSpawnEngine();
            var mission = fixture.CreateMission(client);
            int previousOption = BannerlordConfig.ReinforcementWaveCount;
            try
            {
                BannerlordConfig.ReinforcementWaveCount = localWaveOptionIndex;
                phases = RunJointInit(client.ObjectManager, mission.Shell);
            }
            finally
            {
                BannerlordConfig.ReinforcementWaveCount = previousOption;
            }
        });
        return phases;
    }

    /// <summary>
    /// The client owns the whole attacker side (ShoT_UP's shape); the defender side is owned by another client,
    /// so this one holds an empty defender reserve that still carries the server's side total.
    /// </summary>
    private static List<MissionSpawnPhase>[] RunJointInit(IObjectManager objectManager, Mission mission)
    {
        var defender = new CoopTroopSupplier("M2927", BattleSideEnum.Defender, objectManager, new BattleAgentBudget());
        var attacker = new CoopTroopSupplier("M2927", BattleSideEnum.Attacker, objectManager, new BattleAgentBudget());
        var entries = Enumerable.Range(0, AttackerOwned)
            .Select(i => new TroopReserveEntry(1000 + i, "unresolvable-character", formationClass: 0))
            .ToArray();
        attacker.SetReserve(new[] { new PartyReserve("attacker-party", 0, entries, isReceiverPlayerParty: true) },
            sideTotal: AttackerOwned, playerOwnedParties: 1, authoritativeBattleSize: ServerBattleSize,
            snapshotRevision: 1);
        defender.SetReserve(Array.Empty<PartyReserve>(), sideTotal: DefenderSideTotal, playerOwnedParties: 1,
            authoritativeBattleSize: ServerBattleSize, snapshotRevision: 1);

        // The native constructor needs the scene timer; the joint Init only needs the fields set here.
        var spawnLogic = ObjectHelper.SkipConstructor<DefaultBattleMissionAgentSpawnLogic>();
        AccessTools.Field(typeof(DefaultBattleMissionAgentSpawnLogic), "_phases").SetValue(spawnLogic,
            new[] { new List<MissionSpawnPhase>(), new List<MissionSpawnPhase>() });
        AccessTools.Field(typeof(DefaultBattleMissionAgentSpawnLogic), "_numberOfTroopsInTotal").SetValue(spawnLogic, new int[2]);
        AccessTools.Field(typeof(DefaultBattleMissionAgentSpawnLogic), "_battleSideSpawnContexts").SetValue(spawnLogic,
            new[] { ObjectHelper.SkipConstructor<MissionBattleSideSpawnContext>(),
                ObjectHelper.SkipConstructor<MissionBattleSideSpawnContext>() });
        AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(spawnLogic, mission);
        AccessTools.Field(typeof(Mission), "_initialAgentCountPerSide").SetValue(mission, new int[2]);

        var handler = new CoopBattleMissionSpawnHandler(defender, attacker, null, BattleSideEnum.Attacker);
        AccessTools.Field(typeof(CoopBattleMissionSpawnHandler), "_missionAgentSpawnLogic").SetValue(handler, spawnLogic);

        var sizing = new CoopBattleMissionSpawnHandler.SideSizing(true, true, DefenderSideTotal, AttackerOwned,
            ServerBattleSize);
        AccessTools.Method(typeof(CoopBattleMissionSpawnHandler), "RunJointInit").Invoke(handler, new object[] { sizing });

        // Mid-battle reserve refreshes recompute lifetime quotas from the settings Init stored, so they must hold
        // the shared policy too.
        Assert.Equal(CoopBattleMissionSpawnHandler.SharedMaximumReinforcementWaveCount,
            spawnLogic.SpawnSettings.MaximumReinforcementWaveCount);

        return spawnLogic._phases;
    }

    /// <summary>
    /// Lets the engine's option store and spawn-phase bookkeeping run headless: the option store's static
    /// initializer asks the native layer for the system language, and enabling a side's spawner would start
    /// native deployment. Patches are scoped to one test.
    /// </summary>
    private sealed class HeadlessSpawnEngine : IDisposable
    {
        private readonly Harmony harmony = new Harmony("e2e.tests.battle-shared-wave-policy");

        public HeadlessSpawnEngine()
        {
            harmony.Patch(AccessTools.Method(typeof(TaleWorlds.Engine.Utilities), "GetSystemLanguage"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(HeadlessSpawnEngine), nameof(SystemLanguage))));
            harmony.Patch(AccessTools.Method(typeof(DefaultBattleMissionAgentSpawnLogic), "CheckDeployment"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(HeadlessSpawnEngine), nameof(SkipDeployment))));
        }

        private static bool SystemLanguage(ref string __result)
        {
            __result = "English";
            return false;
        }

        private static bool SkipDeployment(ref bool __result)
        {
            __result = false;
            return false;
        }

        public void Dispose() => harmony.UnpatchAll(harmony.Id);
    }
}