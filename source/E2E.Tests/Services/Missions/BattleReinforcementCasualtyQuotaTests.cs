using System;
using System.Collections.Generic;
using System.Linq;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-073 (Reinforcement Spawning): reinforcements spawn during battle when eligible troops remain. The live
/// stall (battle 406, 2026-07-15 21:25): after the initial battle-size allotment (600 per client), no
/// reinforcement ever spawned while 1400 eligible reserves remained. The engine's wave formula is driven
/// entirely by the supplier's casualty counter — <c>MissionBattleSideSpawnContext.NumberOfActiveTroops =
/// _numSpawnedTroops - supplier.NumRemovedTroops</c>, and <c>ComputeWaveBatch</c> only requests a wave once
/// <c>InitialSpawnedNumber - NumberActiveTroops</c> reaches the wave size. The native feedback chain is
/// <c>BattleAgentLogic.OnAgentRemoved → origin.SetKilled/SetWounded/SetRouted → supplier.OnTroop*</c>; the
/// coop prefix (<see cref="BattleAgentLogicHitRewardPatch.OnAgentRemovedPrefix"/>) faithfully calls the origin
/// hooks, but <see cref="CoopAgentOrigin"/>'s three casualty hooks are empty, so
/// <see cref="CoopTroopSupplier.NumRemovedTroops"/> is frozen at 0 and the engine never asks for another
/// troop. These tests pin the missing origin→supplier feedback (seed-scoped, one-shot per origin) and the
/// native contract the fix relies on.
/// </summary>
public class BattleReinforcementCasualtyQuotaTests : MissionTestEnvironment
{
    public BattleReinforcementCasualtyQuotaTests(ITestOutputHelper output) : base(output) { }

    private static TroopReserveEntry[] Entries(string characterId, int count, int seedBase = 500)
    {
        var entries = new TroopReserveEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = new TroopReserveEntry(seedBase + i, characterId, formationClass: 0);
        return entries;
    }

    /// <summary>A populated supplier whose origins resolve a real registered character. The party id is
    /// deliberately unresolvable — the casualty hooks under test don't need a party, and the origins then
    /// take the simple (party-less) path through the removal prefix.</summary>
    private static CoopTroopSupplier CreateSuppliedSupplier(IObjectManager objectManager, string characterId, int reserveCount)
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, objectManager, new BattleAgentBudget());
        supplier.SetReserve(new[] { new PartyReserve("unresolvable-party", 0, Entries(characterId, reserveCount)) });
        return supplier;
    }

    [Fact]
    [Trait("Requirement", "BR-031")]
    public void MigrationRecovery_ClaimsMissingOrigins_WhenServerPointerIsAlreadyExhausted()
    {
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, client.ObjectManager, new BattleAgentBudget());
            supplier.SetReserve(new[]
            {
                new PartyReserve("unresolvable-party", suppliedCount: 10, entries: Entries(characterId, count: 10)),
            });

            var origins = supplier.ClaimRecoveryTroops(
                "unresolvable-party",
                new Dictionary<string, int> { [characterId] = 3 },
                new HashSet<int> { 500, 501, 502 });

            Assert.Equal(3, origins.Count);
            Assert.Equal(new[] { 500, 501, 502 }, origins.Select(origin => origin.UniqueSeed));
            Assert.Equal(10, Assert.Single(supplier.GetSuppliedByParty()).supplied);
        });
    }

    /// <summary>
    /// The exact call the coop death path makes for a kill — <c>origin.SetKilled()</c>, as
    /// <c>OnAgentRemovedPrefix</c> does for <c>AgentState.Killed</c> — must advance the supplier's removed
    /// quota, exactly once per origin (the one-shot latch native <c>PartyGroupAgentOrigin</c> has via
    /// <c>_isRemoved</c>). RED today: <c>CoopAgentOrigin.SetKilled</c> is an empty body, so the counter
    /// stays 0 for the whole battle.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-073")]
    public void OwnTroopKilled_AdvancesSupplierRemovedQuota()
    {
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            var supplier = CreateSuppliedSupplier(client.ObjectManager, characterId, reserveCount: 10);
            var origins = supplier.SupplyTroops(3).Cast<CoopAgentOrigin>().ToList();
            Assert.Equal(3, origins.Count);
            Assert.Equal(0, supplier.NumRemovedTroops);

            origins[0].SetKilled();
            Assert.Equal(1, supplier.NumRemovedTroops);

            // One-shot per origin: a duplicate removal report must not double-count the same troop.
            origins[0].SetKilled();
            Assert.Equal(1, supplier.NumRemovedTroops);
        });
    }

    /// <summary>
    /// The two non-kill removal states the prefix routes (<c>Unconscious → SetWounded</c>, otherwise
    /// <c>SetRouted</c>) must advance the same quota — <c>NumRemovedTroops</c> is wounded+killed+routed.
    /// RED today: both hooks are empty bodies.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-073")]
    public void OwnTroopWoundedAndRouted_AdvanceSupplierRemovedQuota()
    {
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            var supplier = CreateSuppliedSupplier(client.ObjectManager, characterId, reserveCount: 10);
            var origins = supplier.SupplyTroops(3).Cast<CoopAgentOrigin>().ToList();
            Assert.Equal(3, origins.Count);

            origins[0].SetWounded();
            origins[1].SetRouted(false);
            Assert.Equal(2, supplier.NumRemovedTroops);
        });
    }

    /// <summary>
    /// The full replicated-death seam, not just the origin in isolation: an agent whose origin came from the
    /// side's <see cref="CoopTroopSupplier"/> is removed as Killed through the REAL coop prefix
    /// (<see cref="BattleAgentLogicHitRewardPatch.OnAgentRemovedPrefix"/>, which fires for native and
    /// replicated removals alike) — the supplier's quota must advance. RED today: the prefix calls
    /// <c>Origin.SetKilled()</c>, which is a no-op on <see cref="CoopAgentOrigin"/>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-073")]
    public void AgentRemovedPrefix_KilledState_ReachesSupplierQuota()
    {
        using var fixture = new MissionEngineFixture();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var supplier = CreateSuppliedSupplier(client.ObjectManager, characterId, reserveCount: 5);
            var origin = (CoopAgentOrigin)supplier.SupplyTroops(1).Single();

            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            var team = new MockTeam(BattleSideEnum.Defender);
            var agent = mock.SpawnAgent(new AgentBuildData(character)
                .Controller(AgentControllerType.AI).Team(team.Shell).TroopOrigin(origin));

            BattleAgentLogicHitRewardPatch.OnAgentRemovedPrefix(null, agent, null, AgentState.Killed, default);

            Assert.Equal(1, supplier.NumRemovedTroops);
        });
    }

    /// <summary>
    /// The seed-scoping invariant the fix must keep (green today, must STAY green): a casualty on an origin
    /// the local supplier does NOT own — a puppet origin, built exactly as <c>PuppetSpawner</c> /
    /// <c>ReinforcementFielder</c> build them, with no supplier linkage and a foreign seed — must not count
    /// against the local side's quota. On the non-fielding client that side's <c>_numSpawnedTroops</c> is 0,
    /// so counting puppet deaths would drive <c>NumberOfActiveTroops</c> negative and corrupt
    /// <c>IsSideDepleted</c> and the wave math.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-073")]
    public void PuppetOriginCasualty_DoesNotCountAgainstForeignSupplier()
    {
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            var supplier = CreateSuppliedSupplier(client.ObjectManager, characterId, reserveCount: 2);
            supplier.SupplyTroops(2);

            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));

            // A foreign owner's puppet: no supplier linkage, a seed the local supplier does not own.
            var puppetOrigin = new CoopAgentOrigin(character, null, -1, null, new UniqueTroopDescriptor(999999));
            puppetOrigin.SetKilled();
            puppetOrigin.SetWounded();
            puppetOrigin.SetRouted(false);

            Assert.Equal(0, supplier.NumRemovedTroops);
        });
    }

    /// <summary>
    /// The reinforcement-pull assertion, against the REAL native per-side engine: a
    /// <see cref="MissionBattleSideSpawnContext"/> wrapped around a real <see cref="CoopTroopSupplier"/> that
    /// spawned its 600-troop initial allotment (the live BattleSize), with the sandbox Wave settings
    /// (wavePct 0.5). Casualties through the coop death path must open the wave gate at 300 — the batch the
    /// engine then reserves via <c>SupplyTroops</c> and spawns with <c>isReinforcement: true</c> — and stay
    /// closed at 299. RED today: the batch is 0 at ANY casualty count, because the coop death path never
    /// moves <c>NumRemovedTroops</c>, so the engine believes all 600 initials are still standing forever.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-073")]
    public void WaveBatchGate_UnlocksWhenRemovedQuotaReachesWaveSize()
    {
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            // The live shape: 1500 owned, 600 spawned initially (BattleSize option 600), 900 wave-eligible.
            var supplier = CreateSuppliedSupplier(client.ObjectManager, characterId, reserveCount: 1500);
            var origins = supplier.SupplyTroops(600).Cast<CoopAgentOrigin>().ToList();
            Assert.Equal(600, origins.Count);

            // The sandbox battle wave settings (SandBoxBattleMissionSpawnHandler.CreateSandBoxBattleWaveSpawnSettings).
            var settings = new MissionSpawnSettings(
                MissionSpawnSettings.InitialSpawnMethod.BattleSizeAllocating,
                MissionSpawnSettings.ReinforcementTimingMethod.GlobalTimer,
                MissionSpawnSettings.ReinforcementSpawnMethod.Wave,
                globalReinforcementInterval: 3f,
                reinforcementWavePercentage: 0.5f,
                maximumReinforcementWaveCount: 3);

            var context = new MissionBattleSideSpawnContext(
                new StubBattleSpawnLogic(settings), BattleSideEnum.Defender, supplier, isPlayerSide: true);
            context._numSpawnedTroops = 600; // the initial allotment this side spawned

            var phase = new MissionSpawnPhase { InitialSpawnedNumber = 600, RemainingSpawnNumber = 900 };

            // 299 casualties through the coop death path: the wave gate stays closed (wave size is 300)...
            for (int i = 0; i < 299; i++)
                origins[i].SetKilled();
            phase.NumberActiveTroops = context.NumberOfActiveTroops; // PhaseTick's per-tick copy
            Assert.Equal(0, context.ComputeWaveBatch(phase));

            // ...and the 300th opens it: the engine's next pull requests a full 300-troop wave.
            origins[299].SetKilled();
            phase.NumberActiveTroops = context.NumberOfActiveTroops;
            Assert.Equal(300, context.ComputeWaveBatch(phase));
        });
    }

    /// <summary>
    /// Contract lock-in (green today): the native engine reads casualties EXCLUSIVELY through
    /// <c>IMissionTroopSupplier.NumRemovedTroops</c> — <c>NumberOfActiveTroops = _numSpawnedTroops -
    /// supplier.NumRemovedTroops</c>. Pins the feedback contract the whole BR-073 fix relies on, so a game
    /// update that changes it fails loudly here instead of silently re-stalling reinforcements.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-073")]
    public void NativeContract_NumberOfActiveTroops_ReadsSupplierNumRemovedTroops()
    {
        var supplier = new StubTroopSupplier();
        var context = new MissionBattleSideSpawnContext(
            new StubBattleSpawnLogic(MissionSpawnSettings.CreateDefaultSpawnSettings()),
            BattleSideEnum.Defender, supplier, isPlayerSide: true);
        context._numSpawnedTroops = 600;

        Assert.Equal(600, context.NumberOfActiveTroops);

        supplier.RemovedTroops = 250;
        Assert.Equal(350, context.NumberOfActiveTroops);
    }

    /// <summary>Minimal spawn-logic stand-in for the per-side context: only <c>SpawnSettings</c> is read by
    /// the wave-batch computation under test.</summary>
    private sealed class StubBattleSpawnLogic : IBattleMissionAgentSpawnLogic
    {
        private readonly MissionSpawnSettings settings;

        public StubBattleSpawnLogic(MissionSpawnSettings settings) => this.settings = settings;

        public int TotalSpawnNumber => 0;
        public int BattleSize => 0;
        public int NumberOfAgents => 0;
        public MissionSpawnPhase DefenderActivePhase => null!;
        public MissionSpawnPhase AttackerActivePhase => null!;
        public ref readonly MissionSpawnSettings SpawnSettings => ref settings;
        public IMissionDeploymentPlan DeploymentPlan => null!;
        public BattleSideEnum PlayerSide => BattleSideEnum.Defender;
        public void StartSpawner(BattleSideEnum side) { }
        public void StopSpawner(BattleSideEnum side) { }
        public bool IsSideSpawnEnabled(BattleSideEnum side) => true;
        public bool IsSideDepleted(BattleSideEnum side) => false;
        public float GetReinforcementInterval(BattleSideEnum side = BattleSideEnum.None) => 3f;
        public IEnumerable<IAgentOriginBase> GetAllTroopsForSide(BattleSideEnum side) => Array.Empty<IAgentOriginBase>();
        public bool GetSpawnHorses(BattleSideEnum side) => false;
        public int GetNumberOfPlayerControllableTroops() => 0;
    }

    /// <summary>Supplier stand-in with a directly settable removed count, for pinning the native formula.</summary>
    private sealed class StubTroopSupplier : IMissionTroopSupplier
    {
        public int RemovedTroops;

        public int NumRemovedTroops => RemovedTroops;
        public int NumTroopsNotSupplied => 0;
        public bool AnyTroopRemainsToBeSupplied => false;
        public IEnumerable<IAgentOriginBase> SupplyTroops(int numberToAllocate) => Array.Empty<IAgentOriginBase>();
        public IAgentOriginBase SupplyOneTroop() => null!;
        public IEnumerable<IAgentOriginBase> GetAllTroops() => Array.Empty<IAgentOriginBase>();
        public BasicCharacterObject GetGeneralCharacter() => null!;
        public int GetNumberOfPlayerControllableTroops() => 0;
    }
}
