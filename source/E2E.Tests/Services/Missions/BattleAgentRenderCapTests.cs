using System;
using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Surrogates;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-110 (Maximum Concurrent Agents): the Bannerlord engine can only render a maximum of 2000 agents, so no
/// coop spawn path — supplier wave pulls, mid-battle reinforcement parties, or replicated puppets — may push a
/// client's live agent count past that limit. Withheld troops are deferred, not lost: puppets stay buffered
/// and drain as removals free capacity, withheld reinforcement troops field on tick, and unallocated wave
/// troops stay unsupplied (wave-eligible) in the reserve. RED today: none of the spawn paths consults any
/// mission-wide agent count, so each spawns straight past the limit.
/// </summary>
public class BattleAgentRenderCapTests : MissionTestEnvironment
{
    /// <summary>The engine's render ceiling per BR-110 — asserted as the literal requirement value, not the
    /// production constant, so these tests pin the requirement itself.</summary>
    private const int EngineAgentLimit = 2000;

    public BattleAgentRenderCapTests(ITestOutputHelper output) : base(output) { }

    private static int CountLiveAgents(MockMission mock)
        => mock.Agents.Count(agent => AgentMirror.TryGet(agent, out var mirror) && mirror.IsActive);

    /// <summary>Spawns inert filler agents until the mock mission holds <paramref name="liveTarget"/> live
    /// agents, simulating a battle already rendering that many.</summary>
    private static void FloodToLiveCount(MockMission mock, int liveTarget)
    {
        var character = (CharacterObject)Game.Current.PlayerTroop;
        int missing = liveTarget - CountLiveAgents(mock);
        for (int i = 0; i < missing; i++)
            mock.SpawnAgent(new AgentBuildData(character)
                .Controller(AgentControllerType.None)
                .Team(mock.DefenderTeam.Shell));
    }

    /// <summary>Marks <paramref name="count"/> live agents inactive — the harness equivalent of agents being
    /// removed from the mission, freeing render capacity.</summary>
    private static void DeactivateAgents(MockMission mock, int count)
    {
        foreach (var agent in mock.Agents)
        {
            if (count == 0) return;
            if (AgentMirror.TryGet(agent, out var mirror) && mirror.IsActive)
            {
                mirror.IsActive = false;
                count--;
            }
        }
    }

    private static IReinforcementFielder GetFielder(CoopBattleController controller)
        => (IReinforcementFielder)AccessTools.Field(typeof(CoopBattleController), "reinforcementFielder").GetValue(controller);

    private static IPuppetSpawner GetPuppetSpawner(CoopBattleController controller)
        => (IPuppetSpawner)AccessTools.Field(typeof(CoopBattleController), "puppetSpawner").GetValue(controller);

    /// <summary>Adds an AI party with <paramref name="troopCount"/> able troops to the battle's defender side
    /// (registered everywhere, roster populated on the host). Returns its MapEventParty id.</summary>
    private string AddAiReinforcementParty(string mapEventId, EnvironmentInstance host, int troopCount)
    {
        var aiPartyId = CreateRegisteredObject<MobileParty>();
        string mapEventPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var aiParty));

            aiParty.Party.MapEventSide = mapEvent.DefenderSide;

            var mep = mapEvent.DefenderSide.Parties.Last(p => p.Party == aiParty.Party);
            Assert.True(Server.ObjectManager.TryGetId(mep, out mapEventPartyId));
        }, MapEventDisabledMethods);

        host.Call(() =>
        {
            Assert.True(host.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var aiParty));
            aiParty.Party.MemberRoster.Clear();
            aiParty.Party.MemberRoster.AddToCounts((CharacterObject)Game.Current.PlayerTroop, troopCount);
        });

        Assert.NotNull(mapEventPartyId);
        return mapEventPartyId;
    }

    private static void PublishInvolvedPartiesAdded(EnvironmentInstance instance, string mapEventId, string mapEventPartyId)
    {
        instance.Resolve<IMessageBroker>().Publish(instance,
            new NetworkAddInvolvedParties(mapEventId, new[] { mapEventPartyId }, new[] { new CampaignVec2(default, true) }));
    }

    private static TroopReserveEntry[] Entries(string characterId, int count, int seedBase = 500)
    {
        var entries = new TroopReserveEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = new TroopReserveEntry(seedBase + i, characterId, formationClass: 0);
        return entries;
    }

    private static CoopTroopSupplier CreateSuppliedSupplier(IObjectManager objectManager, string characterId, int reserveCount)
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, objectManager, new BattleAgentBudget());
        supplier.SetReserve(new[] { new PartyReserve("unresolvable-party", 0, Entries(characterId, reserveCount)) });
        return supplier;
    }

    /// <summary>
    /// A new AI party joins a battle already at (near) the engine limit: the host fields only the troops that
    /// fit under the limit and never spawns past it. RED today: SpawnReinforcementParty loops the full able
    /// roster with no capacity check.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void ReinforcementParty_AtEngineAgentLimit_DoesNotSpawnPastIt()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var host = Clients.First();
        var aiMapEventPartyId = AddAiReinforcementParty(mapEventId, host, troopCount: 5);

        CoopBattleController controller = null;
        MockMission mock = null;
        host.Call(() =>
        {
            mock = fixture.CreateMission(host);
            controller = host.Resolve<CoopBattleController>();
        });

        EnterBattle(host, mapEventId);

        host.Call(() =>
        {
            controller.OnDeploymentFinished();
            FloodToLiveCount(mock, EngineAgentLimit - 2);   // room for only 2 of the party's 5 troops

            PublishInvolvedPartiesAdded(host, mapEventId, aiMapEventPartyId);

            Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));
        });

        GC.KeepAlive(controller);
    }

    /// <summary>
    /// Troops withheld at the limit are deferred, not lost (BR-110, BR-073): as removals free capacity, the
    /// fielder's tick spawns the remainder — never exceeding the limit at any point.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void ReinforcementParty_WithheldTroops_FieldAsCapacityFrees()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var host = Clients.First();
        var aiMapEventPartyId = AddAiReinforcementParty(mapEventId, host, troopCount: 5);

        CoopBattleController controller = null;
        MockMission mock = null;
        host.Call(() =>
        {
            mock = fixture.CreateMission(host);
            controller = host.Resolve<CoopBattleController>();
        });

        EnterBattle(host, mapEventId);

        host.Call(() =>
        {
            controller.OnDeploymentFinished();
            FloodToLiveCount(mock, EngineAgentLimit - 2);
            int flooded = mock.Agents.Count;

            PublishInvolvedPartiesAdded(host, mapEventId, aiMapEventPartyId);
            Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));      // 2 fielded, 3 withheld

            DeactivateAgents(mock, 3);                                  // casualties free 3 slots
            GetFielder(controller).Tick();

            Assert.Equal(flooded + 5, mock.Agents.Count);               // all 5 troops fielded in the end
            Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));      // and the live count never passed the cap

            GetFielder(controller).Tick();                              // queue is drained — nothing double-spawns
            Assert.Equal(flooded + 5, mock.Agents.Count);
        });

        GC.KeepAlive(controller);
    }

    /// <summary>
    /// A puppet record arriving while the mission is at the engine limit is buffered (deferred), not spawned
    /// and not dropped; the drain fields it once removals free capacity. RED today: TrySpawnPuppetNow spawns
    /// unconditionally.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void PuppetSpawn_AtEngineAgentLimit_IsDeferredUntilCapacityFrees()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("host", "peer");
        var host = Clients.First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var agentId = Guid.NewGuid();

        try
        {
            host.Call(() =>
            {
                var mock = fixture.CreateMission(host);
                var controller = host.Resolve<CoopBattleController>();
                var registry = host.Resolve<INetworkAgentRegistry>();
                controller.Session.TryBegin(mapEventId);
                host.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("host", new[] { "peer" }));
                BattleSpawnGate.BeginBattle(mapEventId);

                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var peerParty));
                var mep = peerParty.MapEvent.DefenderSide.Parties.Single(p => p.Party == peerParty.Party);
                Assert.True(host.ObjectManager.TryGetId(mep, out var mapEventPartyId));

                FloodToLiveCount(mock, EngineAgentLimit);

                var record = new BattleAgentSpawnData(
                    agentId, characterId, default, BattleSideEnum.Defender, 100f,
                    "peer", mapEventPartyId, 7, new Equipment(), new BodyProperties(), new MissionEquipmentData(new()));
                host.Resolve<IMessageBroker>().Publish(this, new NetworkSpawnBattleAgents(new[] { record }));

                Assert.False(registry.TryGetAgentInfo(agentId, out _)); // deferred: not spawned at the limit
                Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));

                DeactivateAgents(mock, 1);                              // a removal frees one slot
                GetPuppetSpawner(controller).DrainPendingPuppets();

                Assert.True(registry.TryGetAgentInfo(agentId, out _));  // the buffered puppet fielded
                Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// A NEW PLAYER joins a battle already near the engine limit (BR-005 late join) and reveals their troops
    /// — which reach every existing client as ONE NetworkSpawnBattleAgents batch. The receiving client fields
    /// only the records that fit under the limit; the rest are deferred (buffered, none dropped) and drain
    /// progressively as removals free capacity, until the joiner's whole party is fielded.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    [Trait("Requirement", "BR-005")]
    public void NewPlayerJoins_BattleNearEngineAgentLimit_TroopBatchDefersAndFieldsAsCapacityFrees()
    {
        using var fixture = new MissionEngineFixture();
        // Three players; "joiner" is a registered remote player with a party on the attacker side. This test
        // is the HOST's view of the join, so the joiner needs no client instance — only its mesh traffic.
        var (mapEventId, partyIds) = SetupCoopBattle("host", "peer", "joiner");
        var host = Clients.First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var agentIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();

        try
        {
            host.Call(() =>
            {
                var mock = fixture.CreateMission(host);
                var controller = host.Resolve<CoopBattleController>();
                var registry = host.Resolve<INetworkAgentRegistry>();
                controller.Session.TryBegin(mapEventId);
                host.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("host", new[] { "peer", "joiner" }));
                BattleSpawnGate.BeginBattle(mapEventId);

                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[2], out var joinerParty));
                var mep = joinerParty.MapEvent.AttackerSide.Parties.Single(p => p.Party == joinerParty.Party);
                Assert.True(host.ObjectManager.TryGetId(mep, out var mapEventPartyId));

                FloodToLiveCount(mock, EngineAgentLimit - 5);           // room for only 5 of the joiner's 10 troops

                var records = agentIds.Select((id, i) => new BattleAgentSpawnData(
                    id, characterId, default, BattleSideEnum.Attacker, 100f,
                    "joiner", mapEventPartyId, 100 + i, new Equipment(), new BodyProperties(), new MissionEquipmentData(new()))).ToArray();
                host.Resolve<IMessageBroker>().Publish(this, new NetworkSpawnBattleAgents(records));

                int Registered() => agentIds.Count(id => registry.TryGetAgentInfo(id, out _));

                Assert.Equal(5, Registered());                          // fields exactly what fits...
                Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));  // ...and stops at the limit

                DeactivateAgents(mock, 3);                              // casualties free 3 slots
                GetPuppetSpawner(controller).DrainPendingPuppets();
                Assert.Equal(8, Registered());                          // partial drain: 3 more, 2 still deferred
                Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));

                DeactivateAgents(mock, 2);
                GetPuppetSpawner(controller).DrainPendingPuppets();
                Assert.Equal(10, Registered());                         // the joiner's whole party fielded, none dropped
                Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));

                GetPuppetSpawner(controller).DrainPendingPuppets();     // buffer is empty — nothing double-spawns
                Assert.Equal(10, Registered());
                Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// A mounted puppet record spawns rider AND horse in one SpawnAgent call — two agents, two slots. The mount
    /// is detected from the spawn EQUIPMENT (the horse the engine mints). With only one slot free it must be
    /// deferred; with two free it fields both without passing the limit.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void MountedPuppet_WithOneSlotFree_IsDeferredUntilTwoSlotsFree()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("host", "peer");
        var host = Clients.First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var agentId = Guid.NewGuid();
        var mountAgentId = Guid.NewGuid();

        try
        {
            host.Call(() =>
            {
                var mock = fixture.CreateMission(host);
                var controller = host.Resolve<CoopBattleController>();
                var registry = host.Resolve<INetworkAgentRegistry>();
                controller.Session.TryBegin(mapEventId);
                host.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("host", new[] { "peer" }));
                BattleSpawnGate.BeginBattle(mapEventId);

                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var peerParty));
                var mep = peerParty.MapEvent.DefenderSide.Parties.Single(p => p.Party == peerParty.Party);
                Assert.True(host.ObjectManager.TryGetId(mep, out var mapEventPartyId));

                FloodToLiveCount(mock, EngineAgentLimit - 1);           // one slot free — not enough for two agents
                mock.SpawnMounted = true;                               // a spawn now would mint rider + horse

                var record = new BattleAgentSpawnData(
                    agentId, characterId, default, BattleSideEnum.Defender, 100f,
                    "peer", mapEventPartyId, 7, BattleAgentBudgetTests.MountedEquipment(), new BodyProperties(), new MissionEquipmentData(new()),
                    mountAgentId);
                host.Resolve<IMessageBroker>().Publish(this, new NetworkSpawnBattleAgents(new[] { record }));

                Assert.False(registry.TryGetAgentInfo(agentId, out _)); // deferred: rider + horse would exceed the limit
                Assert.Equal(EngineAgentLimit - 1, CountLiveAgents(mock));

                DeactivateAgents(mock, 1);                              // now two slots free
                GetPuppetSpawner(controller).DrainPendingPuppets();

                Assert.True(registry.TryGetAgentInfo(agentId, out _));
                Assert.True(registry.TryGetAgentInfo(mountAgentId, out _));
                Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// A catch-up puppet record whose original horse already died carries an EMPTY MountAgentId while its spawn
    /// equipment still mounts a fresh horse. The slot count must come from the equipment, not MountAgentId, so
    /// the rider+horse pair is still budgeted as two slots and deferred at the limit rather than pushing to 2001.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void MountedPuppet_WithEmptyMountId_CountsTheHorseFromEquipment()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("host", "peer");
        var host = Clients.First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var agentId = Guid.NewGuid();

        try
        {
            host.Call(() =>
            {
                var mock = fixture.CreateMission(host);
                var controller = host.Resolve<CoopBattleController>();
                var registry = host.Resolve<INetworkAgentRegistry>();
                controller.Session.TryBegin(mapEventId);
                host.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("host", new[] { "peer" }));
                BattleSpawnGate.BeginBattle(mapEventId);

                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var peerParty));
                var mep = peerParty.MapEvent.DefenderSide.Parties.Single(p => p.Party == peerParty.Party);
                Assert.True(host.ObjectManager.TryGetId(mep, out var mapEventPartyId));

                FloodToLiveCount(mock, EngineAgentLimit - 1);           // one slot free
                mock.SpawnMounted = true;                               // equipment mounts a horse → 2 agents

                var record = new BattleAgentSpawnData(
                    agentId, characterId, default, BattleSideEnum.Defender, 100f,
                    "peer", mapEventPartyId, 7, BattleAgentBudgetTests.MountedEquipment(), new BodyProperties(), new MissionEquipmentData(new()),
                    Guid.Empty);                                        // MountAgentId empty despite the mount
                host.Resolve<IMessageBroker>().Publish(this, new NetworkSpawnBattleAgents(new[] { record }));

                Assert.False(registry.TryGetAgentInfo(agentId, out _)); // deferred on the equipment's 2-slot cost
                Assert.Equal(EngineAgentLimit - 1, CountLiveAgents(mock));

                DeactivateAgents(mock, 1);                              // two slots free
                GetPuppetSpawner(controller).DrainPendingPuppets();

                Assert.True(registry.TryGetAgentInfo(agentId, out _));  // rider fielded (its horse is unregistered)
                Assert.Equal(EngineAgentLimit, CountLiveAgents(mock));

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// The native wave pump (SupplyTroops) allocates no more troops than the engine has capacity for; the
    /// unallocated remainder stays UNSUPPLIED so later waves can re-request it as casualties free slots.
    /// RED today: SupplyTroops honors the requested batch size unconditionally.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void SupplierWaveRequest_IsClampedToRemainingEngineCapacity()
    {
        using var fixture = new MissionEngineFixture();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            // Unmounted troops — one render slot each — so this exercises the pure count clamp (the harness's
            // CharacterObjectBuilder fills the Horse slot with a placeholder, which would otherwise read as a mount).
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            character.Equipment[EquipmentIndex.Horse] = default;

            var mock = fixture.CreateMission(client);
            FloodToLiveCount(mock, EngineAgentLimit - 5);

            var supplier = CreateSuppliedSupplier(client.ObjectManager, characterId, reserveCount: 50);
            var origins = supplier.SupplyTroops(30).ToList();

            Assert.Equal(5, origins.Count);                             // only the remaining engine capacity
            Assert.Equal(45, supplier.NumTroopsNotSupplied);            // the rest stays wave-eligible, unconsumed
        });
    }

    /// <summary>At the limit the wave pump supplies nothing — and consumes nothing from the reserve.</summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void SupplierWaveRequest_AtEngineAgentLimit_SuppliesNothing()
    {
        using var fixture = new MissionEngineFixture();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            FloodToLiveCount(mock, EngineAgentLimit);

            var supplier = CreateSuppliedSupplier(client.ObjectManager, characterId, reserveCount: 50);
            var origins = supplier.SupplyTroops(10).ToList();

            Assert.Empty(origins);
            Assert.Equal(50, supplier.NumTroopsNotSupplied);
        });
    }

    /// <summary>
    /// The wave pump's clamp is by RENDER SLOTS, not troop count: a mounted troop spawns a rider and a horse, so
    /// with three slots free it supplies only one mounted troop (the second would need two more) and leaves the
    /// rest unsupplied and wave-eligible — never returning a cavalry origin that would push the mission to 2001.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void SupplierWave_MountedTroops_ClampsBySlotsNotTroopCount()
    {
        using var fixture = new MissionEngineFixture();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            // Give the supplier's character a real mount so each troop costs two render slots.
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            character.Equipment[EquipmentIndex.Horse] = new EquipmentElement(new ItemObject { ItemComponent = new HorseComponent() });
            Assert.True(character.Equipment.Horse.Item.HasHorseComponent, "arrange: the supplier troop must be mounted");

            var mock = fixture.CreateMission(client);
            FloodToLiveCount(mock, EngineAgentLimit - 3);   // three slots free — room for one mounted troop only

            var supplier = CreateSuppliedSupplier(client.ObjectManager, characterId, reserveCount: 50);
            var origins = supplier.SupplyTroops(30).ToList();

            Assert.Single(origins);                          // 1 mounted troop (2 slots); a second would need 2 more
            Assert.Equal(49, supplier.NumTroopsNotSupplied); // the rest stays unsupplied (wave-eligible)
        });
    }

    /// <summary>Reserved-troop origins over <paramref name="character"/> — the FIFO list the native drip
    /// clamp walks; only the troop (and its equipment) matters to slot costing.</summary>
    private static List<IAgentOriginBase> ReservedOrigins(CharacterObject character, int count)
    {
        var origins = new List<IAgentOriginBase>(count);
        for (int i = 0; i < count; i++)
            origins.Add(new SimpleAgentOrigin(character, -1, null, default));
        return origins;
    }

    /// <summary>
    /// BR-110 native drip gate (MissionSpawnCapacityPatch): the reserved wave spawns over ticks via the native
    /// per-side SpawnTroops; each drip is clamped to the LIVE remaining capacity so a wave reserved earlier
    /// cannot spend slots that other paths have since consumed. Unmounted reserved troops cost one slot each;
    /// null mission/budget and non-positive drips pass through untouched.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void NativeDrip_ClampsToLiveRemainingCapacity()
    {
        using var fixture = new MissionEngineFixture();
        var budget = new BattleAgentBudget();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            // Unmounted troops — one render slot each (the harness's CharacterObjectBuilder fills the Horse
            // slot with a placeholder, which would otherwise read as a mount).
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            character.Equipment[EquipmentIndex.Horse] = default;

            var mock = fixture.CreateMission(client);
            var reserved = ReservedOrigins(character, 300);

            Assert.Equal(300, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 300, reserved, true, true)); // empty — unclamped

            FloodToLiveCount(mock, EngineAgentLimit - 5);
            Assert.Equal(5, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 300, reserved, true, true));    // only 5 slots free

            FloodToLiveCount(mock, EngineAgentLimit);
            Assert.Equal(0, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 300, reserved, true, true));    // at the limit

            Assert.Equal(50, MissionSpawnCapacityPatch.ClampSpawnNumber(null, budget, 50, reserved, true, true));          // no mission — passthrough
            Assert.Equal(-1, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, -1, reserved, true, true));    // non-positive — passthrough
        });
    }

    /// <summary>
    /// The drip clamp is by RENDER SLOTS, not troop count: a mounted reserved origin spawns rider AND horse in
    /// one call — two slots — so with a single slot free the drip spawns NOTHING (a troop-count clamp would
    /// return 1 and take the mission to 2001). With two slots free the pair fits.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void NativeDrip_MountedReservedTroop_WithOneSlotFree_SpawnsNothing()
    {
        using var fixture = new MissionEngineFixture();
        var budget = new BattleAgentBudget();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            character.Equipment[EquipmentIndex.Horse] = new EquipmentElement(new ItemObject { ItemComponent = new HorseComponent() });

            var mock = fixture.CreateMission(client);
            var reserved = ReservedOrigins(character, 10);

            FloodToLiveCount(mock, EngineAgentLimit - 1);   // one slot free — not enough for rider + horse
            Assert.Equal(0, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 1, reserved, true, false));

            DeactivateAgents(mock, 1);                      // two slots free — the pair fits
            Assert.Equal(1, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 1, reserved, true, false));

            DeactivateAgents(mock, 3);                      // five free: two mounted troops (4 slots) fit, a third needs one more
            Assert.Equal(2, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 10, reserved, true, false));
        });
    }

    /// <summary>
    /// The drip charges each reserved troop its own cost IN ORDER and stops at the first that does not fit —
    /// it never skips a mounted troop to spawn a cheaper one queued behind it (the reserve spawns FIFO).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void NativeDrip_MixedReserve_StopsAtFirstTroopThatDoesNotFit()
    {
        using var fixture = new MissionEngineFixture();
        var budget = new BattleAgentBudget();
        var footId = CreateRegisteredObject<CharacterObject>();
        var cavalryId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(footId, out var foot));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(cavalryId, out var cavalry));
            // The client-side copies of both characters resolve to ONE shared equipment roster (registry
            // replication interns them), so give the foot its own unmounted roster before mounting the cavalry.
            foot._equipmentRoster = new MBEquipmentRoster
            {
                _equipments = new MBList<Equipment> { new Equipment(Equipment.EquipmentType.Battle) },
            };
            cavalry.Equipment[EquipmentIndex.Horse] = new EquipmentElement(new ItemObject { ItemComponent = new HorseComponent() });
            Assert.Equal(1, budget.SlotsForOrigin(new SimpleAgentOrigin(foot, -1, null, default)));    // arrange: foot = 1 slot
            Assert.Equal(2, budget.SlotsForOrigin(new SimpleAgentOrigin(cavalry, -1, null, default))); // arrange: cavalry = 2 slots

            var mock = fixture.CreateMission(client);
            var reserved = new List<IAgentOriginBase>
            {
                new SimpleAgentOrigin(foot, -1, null, default),
                new SimpleAgentOrigin(cavalry, -1, null, default),
                new SimpleAgentOrigin(foot, -1, null, default),
                new SimpleAgentOrigin(foot, -1, null, default),
            };

            FloodToLiveCount(mock, EngineAgentLimit - 2);   // two free: foot fits (1), the cavalry behind it needs 2 more
            Assert.Equal(1, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 4, reserved, true, false));

            DeactivateAgents(mock, 2);                      // four free: foot(1) + cavalry(2) + foot(1) fit, the last foot does not
            Assert.Equal(3, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 4, reserved, true, false));
        });
    }

    /// <summary>
    /// A side spawning WITHOUT horses (siege assaults dismount everyone) mints no mounts — a mounted troop's
    /// drip costs one slot, so the dismounted wave is not over-withheld.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void NativeDrip_SpawnWithoutHorses_MountedTroopCostsOneSlot()
    {
        using var fixture = new MissionEngineFixture();
        var budget = new BattleAgentBudget();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            character.Equipment[EquipmentIndex.Horse] = new EquipmentElement(new ItemObject { ItemComponent = new HorseComponent() });

            var mock = fixture.CreateMission(client);
            var reserved = ReservedOrigins(character, 5);

            FloodToLiveCount(mock, EngineAgentLimit - 2);   // two slots free — two dismounted riders fit
            Assert.Equal(2, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 5, reserved, spawnWithHorses: false, forceSpawnPlayerMounted: false));
        });
    }

    /// <summary>
    /// A request past the end of the reserve tops up from the supplier — those origins are unknown at clamp
    /// time, so each is charged the mounted worst case (2 slots) and the top-up can never overshoot. (Both
    /// native call sites request within the reserve; this pins the conservative fallback.)
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-110")]
    public void NativeDrip_RequestBeyondReserve_ChargesWorstCaseForUnknownTroops()
    {
        using var fixture = new MissionEngineFixture();
        var budget = new BattleAgentBudget();
        var client = Clients.First();

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            FloodToLiveCount(mock, EngineAgentLimit - 10);  // ten slots free / 2 per unknown troop

            Assert.Equal(5, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 10, new List<IAgentOriginBase>(), true, false));
            Assert.Equal(5, MissionSpawnCapacityPatch.ClampSpawnNumber(mock.Shell, budget, 10, null, true, false));
        });
    }
}
