using System;
using System.Linq;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-051 (Retreating Player Troop Removal): when a player retreats from a coop battle, their troops are
/// removed from the battlefield on every remaining client. The live leak (battle MapEvent_Created_308,
/// 2026-07-15 21:22): in a PVP battle the retreating NON-host's 600 troops and its hero puppet all stayed on
/// the host — <c>BattleAuthorityMigrator.DespawnControllerTroops</c> selects the retreater's agents by
/// comparing each agent's team side to the LOCAL client's <c>PlayerTeam</c> side, which only works when all
/// players fight on the same side. The retreat signal itself was delivered (the server-relayed
/// <see cref="MissionPeerLeft"/>) — only the despawn's target selection is wrong. The host-retreat path
/// (<c>DespawnOwnPartyTroops</c>) already selects by OWNERSHIP (origin party + hero), which is side-agnostic —
/// the fix is to select the non-host retreater's troops the same way.
/// </summary>
public class BattleNonHostRetreatDespawnTests : MissionTestEnvironment
{
    public BattleNonHostRetreatDespawnTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    /// <summary>
    /// The core PVP leak: the local client fights as Defender, the retreater "A" fields its party on the
    /// Attacker team (exactly how <c>PuppetSpawner.ResolvePuppetTeam</c> places an opponent's puppets). A's
    /// graceful retreat must despawn its own-party troops here regardless of which side they fight on.
    /// RED today: <c>agent.Team.Side (Attacker) != playerSide (Defender)</c> skips every one of them —
    /// they stay registered to a controller that no longer answers (inert, effectively unkillable puppets).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-051")]
    public void NonHostRetreat_OpposingSideTroops_AreDespawnedOnRemainingClients()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var remaining = Clients.Skip(1).First(); // "B", the client that stays in the battle

        var troopId = Guid.NewGuid();

        try
        {
            remaining.Call(() =>
            {
                var mock = fixture.CreateMission(remaining);
                var controller = remaining.Resolve<CoopBattleController>();
                var registry = remaining.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                // The LOCAL client is the host; the retreater "A" is a NON-host member.
                remaining.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("B", new[] { "A" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);

                // PVP sides: we fight as Defender; A's puppets replicated onto the opposing Attacker team.
                mock.PlayerTeam = mock.DefenderTeam;

                Assert.True(remaining.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var retreaterParty));
                var character = (CharacterObject)Game.Current.PlayerTroop;

                // A's OWN-party troop (origin party = A's player party), an inert puppet on the Attacker team.
                var origin = new CoopAgentOrigin(character, retreaterParty.Party, -1, null, new UniqueTroopDescriptor(1));
                var troop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell).TroopOrigin(origin));
                Assert.True(registry.TryRegisterAgent("A", troopId, troop));

                // A retreats gracefully — the reliable server-relayed leave, proven delivered in the live session.
                remaining.Resolve<IMessageBroker>().Publish(this, new MissionPeerLeft("A", mapEventId));

                // BR-051: the retreater's troop withdrew — deregistered and faded out — on the remaining client.
                Assert.False(registry.TryGetAgentInfo(troopId, out _),
                    "the retreating non-host's own-party troop must be deregistered on every remaining client");
                Assert.True(AgentMirror.TryGet(troop, out var mirror));
                Assert.False(mirror.IsActive, "the retreating non-host's own-party troop must fade out (withdraw)");

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    [Fact]
    [Trait("Requirement", "BR-051")]
    public void NonHostDisconnect_OpposingSideTroops_AreDespawnedOnRemainingClients()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var remaining = Clients.Skip(1).First();
        var troopId = Guid.NewGuid();
        var lateReplayId = Guid.NewGuid();
        var replayCharacterId = CreateRegisteredObject<CharacterObject>();

        try
        {
            remaining.Call(() =>
            {
                var mock = fixture.CreateMission(remaining);
                var controller = remaining.Resolve<CoopBattleController>();
                var registry = remaining.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                remaining.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("B", new[] { "A" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);
                mock.PlayerTeam = mock.DefenderTeam;

                Assert.True(remaining.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var disconnectedParty));
                var character = (CharacterObject)Game.Current.PlayerTroop;
                var origin = new CoopAgentOrigin(character, disconnectedParty.Party, -1, null, new UniqueTroopDescriptor(101));
                var troop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell).TroopOrigin(origin));
                Assert.True(registry.TryRegisterAgent("A", troopId, troop));

                var broker = remaining.Resolve<IMessageBroker>();
                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));

                Assert.False(registry.TryGetAgentInfo(troopId, out _));
                Assert.True(AgentMirror.TryGet(troop, out var mirror));
                Assert.False(mirror.IsActive, "a disconnected player's party must fade out like a retreat");

                broker.Publish(this, new NetworkSpawnBattleAgents(new[]
                {
                    new BattleAgentSpawnData(lateReplayId, replayCharacterId, default, BattleSideEnum.Attacker,
                        100f, "A", null, 106, new Equipment(), new BodyProperties(), new(new()), default),
                }));
                Assert.False(registry.TryGetAgentInfo(lateReplayId, out _),
                    "a non-host's late replay must be dropped even when its party attribution is missing");

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    [Fact]
    [Trait("Requirement", "BR-051")]
    public void Disconnect_LateReplayDropsPlayerParty_ButKeepsHostNpcForces()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var successor = Clients.Skip(1).First();
        var playerAgentId = Guid.NewGuid();
        var npcAgentId = Guid.NewGuid();
        var freshPlayerAgentId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();

        try
        {
            successor.Call(() =>
            {
                var mock = fixture.CreateMission(successor);
                var controller = successor.Resolve<CoopBattleController>();
                var registry = successor.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                successor.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("A", new[] { "B" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);
                mock.PlayerTeam = mock.DefenderTeam;

                Assert.True(successor.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var disconnectedParty));
                Assert.True(successor.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var npcParty));
                var mapEvent = disconnectedParty.MapEvent;
                var parties = mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties);
                var disconnectedMapEventParty = parties.Single(p => p.Party == disconnectedParty.Party);
                var npcMapEventParty = parties.Single(p => p.Party == npcParty.Party);
                Assert.True(successor.ObjectManager.TryGetId(disconnectedMapEventParty, out var disconnectedMapEventPartyId));
                Assert.True(successor.ObjectManager.TryGetId(npcMapEventParty, out var npcMapEventPartyId));

                var broker = successor.Resolve<IMessageBroker>();
                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                broker.Publish(this, new NetworkSpawnBattleAgents(new[]
                {
                    new BattleAgentSpawnData(playerAgentId, characterId, default, BattleSideEnum.Attacker, 100f,
                        "A", disconnectedMapEventPartyId, 102, new Equipment(), new BodyProperties(), new(new()), default),
                    new BattleAgentSpawnData(npcAgentId, characterId, default, BattleSideEnum.Attacker, 100f,
                        "A", npcMapEventPartyId, 103, new Equipment(), new BodyProperties(), new(new()), default),
                }));

                Assert.False(registry.TryGetAgentInfo(playerAgentId, out _),
                    "a late replay must not restore the disconnected player's withdrawn party");
                Assert.True(registry.TryGetAgentInfo(npcAgentId, out _),
                    "a departed host's NPC forces must remain available for successor adoption");

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkSpawnBattleAgents(new[]
                {
                    new BattleAgentSpawnData(freshPlayerAgentId, characterId, default, BattleSideEnum.Attacker, 100f,
                        "A", disconnectedMapEventPartyId, 104, new Equipment(), new BodyProperties(), new(new()), default),
                }));

                Assert.True(registry.TryGetAgentInfo(freshPlayerAgentId, out _),
                    "rejoining must clear the withdrawal guard so the player's freshly spawned party can return");

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// The retreater's HERO puppet, whose origin party did not resolve at spawn: the ownership-based despawn
    /// must still identify it through the hero belt-check (<c>character.IsHero</c> +
    /// <c>character.HeroObject == the retreater's hero</c>, resolved via IPlayerManager/IObjectManager).
    /// RED today: skipped by the same local-side filter (live evidence: hero puppet
    /// CharacterObject_Player2865 remained on the host after testclient's retreat).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-051")]
    public void NonHostRetreat_OpposingHeroPuppet_IsDespawned()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("A", "B");
        var remaining = Clients.Skip(1).First(); // "B"
        var heroCharacterId = CreateRegisteredObject<CharacterObject>();

        var heroAgentId = Guid.NewGuid();

        try
        {
            remaining.Call(() =>
            {
                var mock = fixture.CreateMission(remaining);
                var controller = remaining.Resolve<CoopBattleController>();
                var registry = remaining.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                remaining.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("B", new[] { "A" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);

                mock.PlayerTeam = mock.DefenderTeam;

                // A CharacterObject linked to A's registered player hero (the shape a hero puppet arrives in).
                Assert.True(remaining.Resolve<IPlayerManager>().TryGetPlayer("A", out var player));
                Assert.True(remaining.ObjectManager.TryGetObject<Hero>(player.HeroId, out var retreaterHero));
                Assert.True(remaining.ObjectManager.TryGetObject<CharacterObject>(heroCharacterId, out var heroCharacter));
                using (new AllowedThread())
                {
                    heroCharacter.HeroObject = retreaterHero;
                }

                // Origin party unresolved at spawn — only the hero belt-check can identify this agent as A's.
                var origin = new CoopAgentOrigin(heroCharacter, null, -1, null, new UniqueTroopDescriptor(2));
                var heroAgent = mock.SpawnAgent(new AgentBuildData(heroCharacter)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell).TroopOrigin(origin));
                Assert.True(registry.TryRegisterAgent("A", heroAgentId, heroAgent));

                remaining.Resolve<IMessageBroker>().Publish(this, new MissionPeerLeft("A", mapEventId));

                Assert.False(registry.TryGetAgentInfo(heroAgentId, out _),
                    "the retreating non-host's hero puppet must be deregistered on every remaining client");
                Assert.True(AgentMirror.TryGet(heroAgent, out var mirror));
                Assert.False(mirror.IsActive, "the retreating non-host's hero puppet must fade out (withdraw)");

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// The live 21:22:43 → 21:23:18 sequence: A retreats (leak), re-engages the same battle, and redeploys
    /// fresh spawn records with NEW agent ids. The registry must end up holding exactly the fresh cohort —
    /// the pre-retreat agent must be gone (despawned at retreat), otherwise the fresh deploy stacks on top.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-051")]
    public void NonHostRetreat_ThenReengageRedeploy_DoesNotAccumulateStaleAgents()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var remaining = Clients.Skip(1).First(); // "B"

        var staleId = Guid.NewGuid();
        var freshId = Guid.NewGuid();

        try
        {
            remaining.Call(() =>
            {
                var mock = fixture.CreateMission(remaining);
                var controller = remaining.Resolve<CoopBattleController>();
                var registry = remaining.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                remaining.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("B", new[] { "A" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);

                mock.PlayerTeam = mock.DefenderTeam;

                Assert.True(remaining.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var retreaterParty));
                var character = (CharacterObject)Game.Current.PlayerTroop;
                var broker = remaining.Resolve<IMessageBroker>();

                // Pre-retreat: A's own-party troop on the opposing team.
                var staleOrigin = new CoopAgentOrigin(character, retreaterParty.Party, -1, null, new UniqueTroopDescriptor(3));
                var staleTroop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell).TroopOrigin(staleOrigin));
                Assert.True(registry.TryRegisterAgent("A", staleId, staleTroop));

                // A retreats, then re-engages (the same server-mediated entry that drives the join catch-up).
                broker.Publish(this, new MissionPeerLeft("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));

                // The redeploy: A's fresh spawn record, a NEW agent id (as observed live at 21:23:18).
                var freshOrigin = new CoopAgentOrigin(character, retreaterParty.Party, -1, null, new UniqueTroopDescriptor(4));
                var freshTroop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell).TroopOrigin(freshOrigin));
                Assert.True(registry.TryRegisterAgent("A", freshId, freshTroop));

                // Exactly the fresh cohort remains keyed to A — no stale pre-retreat ghost stacked underneath.
                Assert.False(registry.TryGetAgentInfo(staleId, out _),
                    "the pre-retreat agent must not survive the retreat into the re-engagement");
                Assert.True(AgentMirror.TryGet(staleTroop, out var staleMirror));
                Assert.False(staleMirror.IsActive, "the pre-retreat agent must have been despawned at the retreat");
                var agents = registry.GetAgents("A");
                Assert.Equal(1, agents.Count);
                Assert.Equal(freshId, agents.Single().AgentId);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// Scope guard for the ownership-based rewrite (green today, must STAY green): a retreating non-host can
    /// be HOLDING agents it merely adopted while it was host earlier (registry OriginalOwner = a third
    /// controller). Those are not its own party — the retreat despawn must not fade them out; they stay
    /// registered, pending the absent-controller sweep / adoption. Guards against a fix that despawns
    /// "everything keyed to A".
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-051")]
    [Trait("Requirement", "BR-031")]
    public void NonHostRetreat_ExHostHeldAdoptedAgents_AreNotDespawnedAsOwnParty()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var remaining = Clients.Skip(1).First(); // "B"

        var ownTroopId = Guid.NewGuid();
        var heldAgentId = Guid.NewGuid();

        try
        {
            remaining.Call(() =>
            {
                var mock = fixture.CreateMission(remaining);
                var controller = remaining.Resolve<CoopBattleController>();
                var registry = remaining.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                remaining.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("B", new[] { "A" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);

                mock.PlayerTeam = mock.DefenderTeam;

                Assert.True(remaining.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var retreaterParty));
                var character = (CharacterObject)Game.Current.PlayerTroop;

                // A's own-party troop, fighting alongside us (same side here — ownership, not side, decides).
                var ownOrigin = new CoopAgentOrigin(character, retreaterParty.Party, -1, null, new UniqueTroopDescriptor(5));
                var ownTroop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.DefenderTeam.Shell).TroopOrigin(ownOrigin));
                Assert.True(registry.TryRegisterAgent("A", ownTroopId, ownTroop));

                // An enemy NPC agent A adopted while it was host earlier: OriginalOwner "C", CurrentAuthority "A"
                // (the shape a host-migration adoption's TryTransferAuthority produces).
                var heldAgent = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell));
                Assert.True(registry.TryRegisterAgent("C", heldAgentId, heldAgent));
                Assert.True(registry.TryTransferAuthority("A", heldAgentId));

                remaining.Resolve<IMessageBroker>().Publish(this, new MissionPeerLeft("A", mapEventId));

                // The own-party troop withdrew...
                Assert.False(registry.TryGetAgentInfo(ownTroopId, out _));
                Assert.True(AgentMirror.TryGet(ownTroop, out var ownMirror));
                Assert.False(ownMirror.IsActive);

                // ...but the held (adopted) agent is NOT A's party: it must not be faded out as one.
                Assert.True(registry.TryGetAgentInfo(heldAgentId, out var heldInfo),
                    "an agent the retreater merely HELD by adoption must not be despawned as its own party");
                Assert.Equal("C", heldInfo.OriginalOwner);
                Assert.True(AgentMirror.TryGet(heldAgent, out var heldMirror));
                Assert.True(heldMirror.IsActive);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// Control case (green today, must stay green): a retreating HOST on the OPPOSING side of the local
    /// client. <c>DespawnOwnPartyTroops</c> selects by ownership (origin party + hero), not by side, so the
    /// PVP host retreat already works — proven live at 21:23:38 ("Despawned 600 retreating own-party
    /// troop(s) of host testclient2"). Pins the host branch so the non-host fix cannot disturb it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-051")]
    public void HostRetreat_OwnershipDespawn_StillWorks_PvpSides()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var successor = Clients.Skip(1).First(); // "B"

        var ownPartyTroopId = Guid.NewGuid();
        var npcTroopId = Guid.NewGuid();

        try
        {
            successor.Call(() =>
            {
                var mock = fixture.CreateMission(successor);
                var controller = successor.Resolve<CoopBattleController>();
                var registry = successor.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                successor.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("A", new[] { "B" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);

                // PVP sides: the local client fights as Defender; the host A's agents are all Attackers.
                mock.PlayerTeam = mock.DefenderTeam;

                Assert.True(successor.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var hostParty));
                var character = (CharacterObject)Game.Current.PlayerTroop;

                // A's OWN-party troop — must withdraw on A's retreat, despite being on the opposing side.
                var ownOrigin = new CoopAgentOrigin(character, hostParty.Party, -1, null, new UniqueTroopDescriptor(6));
                var ownTroop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell).TroopOrigin(ownOrigin));
                Assert.True(registry.TryRegisterAgent("A", ownPartyTroopId, ownTroop));

                // An NPC troop A was RUNNING — must be adopted by the promoted successor and keep fighting.
                var npcTroop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell));
                Assert.True(registry.TryRegisterAgent("A", npcTroopId, npcTroop));

                var broker = successor.Resolve<IMessageBroker>();
                broker.Publish(this, new MissionPeerLeft("A", mapEventId));
                broker.Publish(this, new BattleHostMigrated(mapEventId, "A"));

                // A's own-party troop withdrew: faded out and deregistered, NOT adopted.
                Assert.False(registry.TryGetAgentInfo(ownPartyTroopId, out _));
                Assert.True(AgentMirror.TryGet(ownTroop, out var ownMirror));
                Assert.False(ownMirror.IsActive);

                // The NPC troop was adopted: authority moved to us and it fights on as host AI.
                Assert.True(registry.TryGetAgentInfo(npcTroopId, out var npcInfo));
                Assert.Equal("B", npcInfo.CurrentAuthority);
                Assert.True(AgentMirror.TryGet(npcTroop, out var npcMirror));
                Assert.Equal(AgentControllerType.AI, npcMirror.Controller);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    [Fact]
    [Trait("Requirement", "BR-051")]
    public void HostDisconnect_OwnPartyWithdraws_WhileNpcForcesAreAdopted()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var successor = Clients.Skip(1).First();
        var ownPartyTroopId = Guid.NewGuid();
        var npcTroopId = Guid.NewGuid();

        try
        {
            successor.Call(() =>
            {
                var mock = fixture.CreateMission(successor);
                var controller = successor.Resolve<CoopBattleController>();
                var registry = successor.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                successor.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("A", new[] { "B" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);
                mock.PlayerTeam = mock.DefenderTeam;

                Assert.True(successor.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var hostParty));
                var character = (CharacterObject)Game.Current.PlayerTroop;
                var ownOrigin = new CoopAgentOrigin(character, hostParty.Party, -1, null, new UniqueTroopDescriptor(105));
                var ownTroop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell).TroopOrigin(ownOrigin));
                Assert.True(registry.TryRegisterAgent("A", ownPartyTroopId, ownTroop));

                var npcTroop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell));
                Assert.True(registry.TryRegisterAgent("A", npcTroopId, npcTroop));

                var broker = successor.Resolve<IMessageBroker>();
                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                broker.Publish(this, new BattleHostMigrated(mapEventId, "A"));

                Assert.False(registry.TryGetAgentInfo(ownPartyTroopId, out _));
                Assert.True(AgentMirror.TryGet(ownTroop, out var ownMirror));
                Assert.False(ownMirror.IsActive);

                Assert.True(registry.TryGetAgentInfo(npcTroopId, out var npcInfo));
                Assert.Equal("B", npcInfo.CurrentAuthority);
                Assert.True(AgentMirror.TryGet(npcTroop, out var npcMirror));
                Assert.Equal(AgentControllerType.AI, npcMirror.Controller);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    [Fact]
    [Trait("Requirement", "BR-016")]
    public void RejoiningOldHost_CatchupKeepsMigratedNpcUnderCurrentSuccessor()
    {
        using var fixture = new MissionEngineFixture();
        SetupCoopBattle("A", "B");
        var returner = Clients.First();
        var successor = Clients.Skip(1).First();
        var npcTroopId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();

        successor.Call(() =>
        {
            var mock = fixture.CreateMission(successor);
            var controller = successor.Resolve<CoopBattleController>();
            var registry = successor.Resolve<INetworkAgentRegistry>();

            Assert.True(successor.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            var npcTroop = mock.SpawnAgent(new AgentBuildData(character)
                .Controller(AgentControllerType.AI).Team(mock.AttackerTeam.Shell));
            Assert.True(registry.TryRegisterAgent("A", npcTroopId, npcTroop));
            Assert.True(registry.TryTransferAuthority("B", npcTroopId));

            successor.Resolve<IMessageBroker>().Publish(this, new NetworkMissionPeerEntered("A", null));

            var record = returner.InternalMessages.GetMessages<NetworkSpawnBattleAgents>().Last().Agents.Single();
            Assert.Equal(npcTroopId, record.AgentId);
            Assert.Equal("B", record.OwnerControllerId);

            GC.KeepAlive(controller);
        });
    }

    [Fact]
    [Trait("Requirement", "BR-016")]
    public void ConsecutiveHostDisconnects_FinalSuccessorAdoptsNpcForcesFromEarlierHost()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("A", "B", "C");
        var finalSuccessor = Clients.Skip(2).First();
        var npcTroopId = Guid.NewGuid();

        try
        {
            finalSuccessor.Call(() =>
            {
                var mock = fixture.CreateMission(finalSuccessor);
                var controller = finalSuccessor.Resolve<CoopBattleController>();
                var registry = finalSuccessor.Resolve<INetworkAgentRegistry>();
                var hosts = finalSuccessor.Resolve<IBattleHostRegistry>();
                var broker = finalSuccessor.Resolve<IMessageBroker>();

                controller.Session.TryBegin(mapEventId);
                hosts.Set(mapEventId, new BattleHostAssignment("A", new[] { "B", "C" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);
                mock.PlayerTeam = mock.DefenderTeam;

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));

                var character = (CharacterObject)Game.Current.PlayerTroop;
                var npcTroop = mock.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.None).Team(mock.AttackerTeam.Shell));
                Assert.True(registry.TryRegisterAgent("A", npcTroopId, npcTroop));

                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                hosts.Set(mapEventId, new BattleHostAssignment("B", new[] { "C" }, epoch: 2));

                broker.Publish(this, new MissionPeerDisconnected("B", mapEventId));
                hosts.Set(mapEventId, new BattleHostAssignment("C", Array.Empty<string>(), epoch: 3));
                broker.Publish(this, new BattleHostMigrated(mapEventId, "B"));

                Assert.True(registry.TryGetAgentInfo(npcTroopId, out var npcInfo));
                Assert.Equal("C", npcInfo.CurrentAuthority);
                Assert.Equal("A", npcInfo.OriginalOwner);
                Assert.True(AgentMirror.TryGet(npcTroop, out var npcMirror));
                Assert.True(npcMirror.IsActive);
                Assert.Equal(AgentControllerType.AI, npcMirror.Controller);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }
}
