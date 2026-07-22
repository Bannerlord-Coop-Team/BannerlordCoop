using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Issue #1750: mounts have their own stable identity in the agent registry, like riders. A hit on another
/// owner's horse routes by the HORSE's id (pinned to the horse actually struck — immune to the rider swapping
/// mounts in flight), a lethal hit broadcasts the horse's death to every client (no more zombie horses), and
/// the identity survives rider death (masterless horse) and host-migration adoption. Rider-keyed routing
/// (#1728's IsMount flag) remains only as the fallback for an unregistered horse.
/// </summary>
public class BattleMountIdentityTests : MissionTestEnvironment
{
    public BattleMountIdentityTests(ITestOutputHelper output) : base(output) { }

    private static Blow DamagingBlow(int damage = 30) =>
        new Blow(0) { InflictedDamage = damage, DamageType = DamageTypes.Pierce };

    /// <summary>Registers a mounted rider (rider + linked horse) under <paramref name="ownerId"/>.</summary>
    private static (Agent rider, Agent horse) RegisterMountedRider(
        MockMission mock, INetworkAgentRegistry registry, string ownerId, Guid riderId, Guid horseId,
        AgentControllerType controller)
    {
        BasicCharacterObject character = Game.Current.PlayerTroop;
        var rider = mock.SpawnAgent(new AgentBuildData(character).Controller(controller));
        var horse = mock.SpawnMount(rider);
        Assert.True(registry.TryRegisterAgent(ownerId, riderId, rider));
        Assert.True(registry.TryRegisterAgent(ownerId, horseId, horse));
        return (rider, horse);
    }

    [Fact]
    public void MountHit_RoutesByTheHorsesOwnId_AndOnlyTheOwnerAppliesIt()
    {
        using var fixture = new MissionEngineFixture();
        var attacker = Clients.First();          // holds the rider+horse as inert puppets
        var owner = Clients.Skip(1).First();     // controls the real pair
        SetControllerId(attacker, "attacker");
        SetControllerId(owner, "owner");

        var riderId = Guid.NewGuid();
        var horseId = Guid.NewGuid();
        Agent ownerRider = null, ownerHorse = null, puppetHorse = null;
        CoopBattleController ownerController = null, attackerController = null;

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            ownerController = owner.Resolve<CoopBattleController>();
            (ownerRider, ownerHorse) = RegisterMountedRider(
                mock, owner.Resolve<INetworkAgentRegistry>(), "owner", riderId, horseId, AgentControllerType.AI);
        });

        attacker.Call(() =>
        {
            var mock = fixture.CreateMission(attacker);
            attackerController = attacker.Resolve<CoopBattleController>();
            (_, puppetHorse) = RegisterMountedRider(
                mock, attacker.Resolve<INetworkAgentRegistry>(), "owner", riderId, horseId, AgentControllerType.None);

            // What BattleBlowInterceptPatch publishes when a local troop hits a puppet's HORSE: the struck
            // agent itself. The router resolves its registration and routes by the horse's own id.
            attacker.Resolve<IMessageBroker>().Publish(this,
                new BattlePuppetHit(puppetHorse, null, DamagingBlow(), default, isMount: true));
        });

        // The owner's horse took the blow; its rider did not, and the attacker's puppet horse stayed untouched
        // (its life is the owner's to decide).
        Assert.True(AgentMirror.TryGet(ownerHorse, out var ownerHorseMirror));
        Assert.Equal(70f, ownerHorseMirror.Health);
        Assert.True(AgentMirror.TryGet(ownerRider, out var ownerRiderMirror));
        Assert.Equal(100f, ownerRiderMirror.Health);
        Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
        Assert.Equal(100f, puppetHorseMirror.Health);

        GC.KeepAlive(ownerController);
        GC.KeepAlive(attackerController);
    }

    /// <summary>
    /// The #1750 race: the rider dismounts and remounts a DIFFERENT horse inside the routed message's flight
    /// time. Resolution is by the struck horse's own id, so the blow still lands on the original horse — under
    /// #1728's rider-keyed scheme it would have hit the new one.
    /// </summary>
    [Fact]
    public void MountHit_StaysOnTheStruckHorse_WhenTheRiderSwapsMounts()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        SetControllerId(owner, "owner");

        var riderId = Guid.NewGuid();
        var horseAId = Guid.NewGuid();

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            var controller = owner.Resolve<CoopBattleController>();
            var registry = owner.Resolve<INetworkAgentRegistry>();

            var (rider, horseA) = RegisterMountedRider(mock, registry, "owner", riderId, horseAId, AgentControllerType.AI);

            // The rider swaps to a fresh horse while the hit on horse A is in flight.
            var horseB = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(rider, out var riderMirror));
            riderMirror.MountAgent = horseB;

            // The routed hit arrives, addressed to horse A's own id.
            owner.Resolve<IMessageBroker>().Publish(this,
                new NetworkApplyBattleDamage(horseAId, Guid.Empty, DamagingBlow(), default));

            Assert.True(AgentMirror.TryGet(horseA, out var horseAMirror));
            Assert.Equal(70f, horseAMirror.Health); // the horse that was actually struck
            Assert.True(AgentMirror.TryGet(horseB, out var horseBMirror));
            Assert.Equal(100f, horseBMirror.Health); // the swap target is untouched

            GC.KeepAlive(controller);
        });
    }

    /// <summary>An UNregistered horse (e.g. a loose native one) still routes the #1728 way: keyed off its
    /// rider's id, with the owner resolving the rider's current MountAgent at apply time.</summary>
    [Fact]
    public void UnregisteredMountHit_FallsBackToRiderKeyedRouting()
    {
        using var fixture = new MissionEngineFixture();
        var attacker = Clients.First();
        var owner = Clients.Skip(1).First();
        SetControllerId(attacker, "attacker");
        SetControllerId(owner, "owner");

        var riderId = Guid.NewGuid();
        Agent ownerHorse = null;
        CoopBattleController ownerController = null, attackerController = null;

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            ownerController = owner.Resolve<CoopBattleController>();
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var rider = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            ownerHorse = mock.SpawnMount(rider); // NOT registered
            Assert.True(owner.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", riderId, rider));
        });

        attacker.Call(() =>
        {
            var mock = fixture.CreateMission(attacker);
            attackerController = attacker.Resolve<CoopBattleController>();
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var riderPuppet = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            var horsePuppet = mock.SpawnMount(riderPuppet); // NOT registered
            Assert.True(attacker.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", riderId, riderPuppet));

            attacker.Resolve<IMessageBroker>().Publish(this,
                new BattlePuppetHit(horsePuppet, null, DamagingBlow(), default, isMount: true));
        });

        Assert.True(AgentMirror.TryGet(ownerHorse, out var horseMirror));
        Assert.Equal(70f, horseMirror.Health);

        GC.KeepAlive(ownerController);
        GC.KeepAlive(attackerController);
    }

    [Fact]
    public void OwnedCavalrySpawn_RegistersTheMount_AndBroadcastsItsIdWithTheRider()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var peer = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(peer, "peer");

        var characterId = CreateRegisteredObject<CharacterObject>();

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            mock.SpawnMounted = true; // the engine spawns a cavalry rider's horse inside the same SpawnAgent call
            var controller = owner.Resolve<CoopBattleController>();
            var registry = owner.Resolve<INetworkAgentRegistry>();

            Assert.True(owner.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            var rider = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            var horse = rider.MountAgent;
            Assert.NotNull(horse);

            // What BattleAgentSpawnedPatch publishes when WE spawn a troop into the battle.
            owner.Resolve<IMessageBroker>().Publish(this, new AgentSpawnedInBattle(rider));

            // Rider AND horse registered under us, and the broadcast record carries the horse's id so peers
            // register their puppet's horse under the same identity.
            Assert.True(registry.TryGetAgentInfo(rider, out var riderInfo));
            Assert.True(registry.TryGetAgentInfo(horse, out var horseInfo));
            Assert.Equal("owner", horseInfo.CurrentAuthority);

            var record = peer.InternalMessages.GetMessages<NetworkSpawnBattleAgents>().Single().Agents.Single();
            Assert.Equal(riderInfo.AgentId, record.AgentId);
            Assert.Equal(horseInfo.AgentId, record.MountAgentId);

            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void OwnedSpawn_RegistrationConflict_DoesNotBroadcastUnregisteredIdentity()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var peer = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(peer, "peer");

        var characterId = CreateRegisteredObject<CharacterObject>();

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            var controller = owner.Resolve<CoopBattleController>();
            var registry = owner.Resolve<INetworkAgentRegistry>();

            Assert.True(owner.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            var agent = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            var remoteId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("peer", remoteId, agent));
            int messagesBefore = peer.InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>();

            owner.Resolve<IMessageBroker>().Publish(this, new AgentSpawnedInBattle(agent));

            Assert.True(registry.TryGetAgentInfo(agent, out var info));
            Assert.Equal(remoteId, info.AgentId);
            Assert.Equal("peer", info.CurrentAuthority);
            Assert.Equal(messagesBefore, peer.InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());

            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void MountDeath_Broadcasts_KillsThePuppetHorse_AndReportsNoServerCasualty()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var peer = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(peer, "peer");

        var riderId = Guid.NewGuid();
        var horseId = Guid.NewGuid();
        Agent ownerHorse = null, puppetHorse = null;
        CoopBattleController ownerController = null, peerController = null;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            peerController = peer.Resolve<CoopBattleController>();
            (_, puppetHorse) = RegisterMountedRider(
                mock, peer.Resolve<INetworkAgentRegistry>(), "owner", riderId, horseId, AgentControllerType.None);
        });

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            ownerController = owner.Resolve<CoopBattleController>();
            (_, ownerHorse) = RegisterMountedRider(
                mock, owner.Resolve<INetworkAgentRegistry>(), "owner", riderId, horseId, AgentControllerType.AI);

            // The mission death callback publishes this when OUR horse dies, human or not.
            owner.Resolve<IMessageBroker>().Publish(this, new BattleAgentDied(ownerHorse, null, wounded: false));
        });

        // The death was broadcast and the peer's puppet horse died with it — no more zombie horse.
        Assert.Equal(1, peer.InternalMessages.GetMessageCount<NetworkBattleAgentDied>());
        Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
        Assert.False(puppetHorseMirror.IsActive);
        Assert.Equal(0f, puppetHorseMirror.Health);

        // Both sides dropped the horse from their registries...
        owner.Call(() => Assert.False(owner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(horseId, out _)));
        peer.Call(() => Assert.False(peer.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(horseId, out _)));

        // ...and no map-event roster casualty was reported: a horse is not a troop.
        Assert.Equal(0, Server.InternalMessages.GetMessageCount<NetworkRequestBattleCasualty>());

        GC.KeepAlive(ownerController);
        GC.KeepAlive(peerController);
    }

    /// <summary>A dead rider leaves a MASTERLESS horse: its registration (and thus routing + death sync)
    /// must survive the rider's removal from the registry.</summary>
    [Fact]
    public void RiderDeath_LeavesItsHorseRegistered_AndStillRoutable()
    {
        using var fixture = new MissionEngineFixture();
        var attacker = Clients.First();
        var owner = Clients.Skip(1).First();
        SetControllerId(attacker, "attacker");
        SetControllerId(owner, "owner");

        var riderId = Guid.NewGuid();
        var horseId = Guid.NewGuid();
        Agent ownerRider = null, ownerHorse = null, puppetHorse = null;
        CoopBattleController ownerController = null, attackerController = null;

        attacker.Call(() =>
        {
            var mock = fixture.CreateMission(attacker);
            attackerController = attacker.Resolve<CoopBattleController>();
            (_, puppetHorse) = RegisterMountedRider(
                mock, attacker.Resolve<INetworkAgentRegistry>(), "owner", riderId, horseId, AgentControllerType.None);
        });

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            ownerController = owner.Resolve<CoopBattleController>();
            var registry = owner.Resolve<INetworkAgentRegistry>();
            (ownerRider, ownerHorse) = RegisterMountedRider(
                mock, registry, "owner", riderId, horseId, AgentControllerType.AI);

            // The rider dies; its death is broadcast and it leaves the registry — the horse must not.
            owner.Resolve<IMessageBroker>().Publish(this, new BattleAgentDied(ownerRider, null, wounded: false));
            Assert.False(registry.TryGetAgentInfo(riderId, out _));
            Assert.True(registry.TryGetAgentInfo(horseId, out _));
        });

        // The rider-death broadcast killed the attacker's puppet RIDER but left its REGISTERED horse
        // standing — the horse lives on masterless at its owner and dies only through its own broadcast.
        Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
        Assert.True(puppetHorseMirror.IsActive);

        // A later hit on the now-masterless puppet horse still routes by the horse's own id.
        attacker.Call(() =>
        {
            attacker.Resolve<IMessageBroker>().Publish(this,
                new BattlePuppetHit(puppetHorse, null, DamagingBlow(), default, isMount: true));
        });

        Assert.True(AgentMirror.TryGet(ownerHorse, out var horseMirror));
        Assert.Equal(70f, horseMirror.Health);

        GC.KeepAlive(ownerController);
        GC.KeepAlive(attackerController);
    }

    /// <summary>A rider-death broadcast dismounts the puppet and leaves the horse standing, even an unregistered
    /// one, which simply stays a local loose horse.</summary>
    [Fact]
    public void RiderDeathBroadcast_DismountsThePuppet_AndLeavesItsHorseStanding()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var peer = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(peer, "peer");

        var riderId = Guid.NewGuid();
        Agent riderPuppet = null, puppetHorse = null;
        CoopBattleController ownerController = null, peerController = null;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            peerController = peer.Resolve<CoopBattleController>();
            BasicCharacterObject character = Game.Current.PlayerTroop;
            riderPuppet = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            puppetHorse = mock.SpawnMount(riderPuppet); // NOT registered
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", riderId, riderPuppet));
        });

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            ownerController = owner.Resolve<CoopBattleController>();
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var rider = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            Assert.True(owner.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", riderId, rider));

            owner.Resolve<IMessageBroker>().Publish(this, new BattleAgentDied(rider, null, wounded: false));
        });

        // The puppet rider died dismounted; its horse was not taken along.
        Assert.True(AgentMirror.TryGet(riderPuppet, out var riderMirror));
        Assert.False(riderMirror.IsActive);
        Assert.Null(riderMirror.MountAgent);
        Assert.True(AgentMirror.TryGet(puppetHorse, out var horseMirror));
        Assert.True(horseMirror.IsActive);
        Assert.Null(horseMirror.RiderAgent);

        GC.KeepAlive(ownerController);
        GC.KeepAlive(peerController);
    }

    /// <summary>A joiner replay built while the rider happens to be DISMOUNTED must still carry the horse the
    /// rider spawned with — the joiner's puppet spawns a horse from the rider's equipment either way, and must
    /// map it to the id the rest of the battle routes by (else its death broadcast never lands there).</summary>
    [Fact]
    public void JoinerReplay_CarriesTheDismountedRidersSpawnHorseId()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var joiner = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(joiner, "joiner");

        var characterId = CreateRegisteredObject<CharacterObject>();

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            mock.SpawnMounted = true;
            var controller = owner.Resolve<CoopBattleController>();
            var registry = owner.Resolve<INetworkAgentRegistry>();

            Assert.True(owner.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            var rider = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            var horse = rider.MountAgent;
            owner.Resolve<IMessageBroker>().Publish(this, new AgentSpawnedInBattle(rider));

            Assert.True(registry.TryGetAgentInfo(rider, out var riderInfo));
            Assert.True(registry.TryGetAgentInfo(horse, out var horseInfo));

            // The player dismounts, then a client joins mid-battle: the replay record must still carry the
            // (still-registered, riderless) spawn horse's id. (The joiner also saw the original capture
            // broadcast — the mesh connects everyone up front in this harness — so assert on the REPLAY,
            // the last spawn message it received.)
            rider.MountAgent = null;
            owner.Resolve<IMessageBroker>().Publish(this, new NetworkMissionPeerEntered("joiner", null));

            var record = joiner.InternalMessages.GetMessages<NetworkSpawnBattleAgents>().Last().Agents.Single();
            Assert.Equal(riderInfo.AgentId, record.AgentId);
            Assert.Equal(horseInfo.AgentId, record.MountAgentId);

            GC.KeepAlive(controller);
        });
    }

    /// <summary>The owner broadcasts a registered horse's OWN movement only while nothing rides it — a ridden
    /// horse's pose travels in its rider's MountData, but a masterless one has no other driver.</summary>
    [Fact]
    public void MovementBroadcast_SkipsRiddenHorses_AndIncludesMasterlessOnes()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        SetControllerId(owner, "owner");

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var rider = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            var riddenHorse = mock.SpawnMount(rider);
            var looseHorse = mock.SpawnMount();

            Assert.True(AgentMovementHandler.ShouldBroadcastMovement(rider));
            Assert.False(AgentMovementHandler.ShouldBroadcastMovement(riddenHorse));
            Assert.True(AgentMovementHandler.ShouldBroadcastMovement(looseHorse));

            // A dead rider no longer drives its horse — the owner takes over broadcasting it.
            Assert.True(AgentMirror.TryGet(rider, out var riderMirror));
            riderMirror.IsActive = false;
            Assert.True(AgentMovementHandler.ShouldBroadcastMovement(riddenHorse));
        });
    }

    /// <summary>A masterless horse's own movement packet drives the puppet horse on peers — the owner stays
    /// movement-authoritative over its horse after dismount.</summary>
    [Fact]
    public void MasterlessHorsePacket_DrivesThePuppetHorse()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var horseId = Guid.NewGuid();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var component = peer.Resolve<ICoopMissionComponent>();
            var registry = peer.Resolve<INetworkAgentRegistry>();

            // Our local copy of another owner's masterless horse.
            var puppetHorse = mock.SpawnMount();
            Assert.True(registry.TryRegisterAgent("owner", horseId, puppetHorse));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetMirror));
            puppetMirror.Controller = AgentControllerType.AI;

            // The owner's copy of that horse, with live movement state — the source of the packet.
            var remoteHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(remoteHorse, out var remoteMirror));
            remoteMirror.Position = new Vec3(10f, 20f, 0f);
            remoteMirror.LookDirection = new Vec3(0f, 1f, 0f);
            remoteMirror.MovementDirection = new Vec2(0.6f, 0.8f);
            remoteMirror.InputVector = new Vec2(0.3f, 0.7f);

            var packet = new MountMovementPacket(new[] { horseId }, new[] { new AgentMountData(remoteHorse) });
            component.AgentMovementHandler.MountMovementApplier.HandlePacket(null, packet);

            // The packet's movement input landed on the puppet horse (position itself is reconciled per-frame
            // by the interpolator, which this packet also fed).
            Assert.Equal(AgentControllerType.None, puppetMirror.Controller);
            Assert.Equal(remoteMirror.LookDirection, puppetMirror.LookDirection);
            Assert.Equal(remoteMirror.MovementDirection, puppetMirror.MovementDirection);
            Assert.Equal(remoteMirror.InputVector, puppetMirror.InputVector);
        });
    }

    /// <summary>Host-migration adoption moves a mount's AUTHORITY to the new host (so routing and death sync
    /// keep answering) but must not turn the horse into an AI combatant with a formation.</summary>
    [Fact]
    public void Adoption_TransfersMountAuthority_WithoutConvertingItToAi()
    {
        using var fixture = new MissionEngineFixture();
        var newHost = Clients.First();
        SetControllerId(newHost, "B");

        var npcId = Guid.NewGuid();
        var horseId = Guid.NewGuid();

        newHost.Call(() =>
        {
            var mock = fixture.CreateMission(newHost);
            var controller = newHost.Resolve<CoopBattleController>();
            var registry = newHost.Resolve<INetworkAgentRegistry>();

            controller.Session.TryBegin("mapEvent1");

            // A mounted NPC the old host "A" was running, replicated here as puppets (rider + horse).
            var team = new MockTeam(BattleSideEnum.Attacker);
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var npc = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
            var horse = mock.SpawnMount(npc);
            Assert.True(registry.TryRegisterAgent("A", npcId, npc));
            Assert.True(registry.TryRegisterAgent("A", horseId, horse));

            newHost.Resolve<IMessageBroker>().Publish(this, new BattleHostMigrated("mapEvent1", "A"));

            // The rider was adopted as an AI combatant; the horse only changed authority.
            Assert.True(AgentMirror.TryGet(npc, out var npcMirror));
            Assert.Equal(AgentControllerType.AI, npcMirror.Controller);
            Assert.NotNull(npcMirror.Formation);

            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            Assert.Equal(AgentControllerType.None, horseMirror.Controller);
            Assert.Null(horseMirror.Formation);

            Assert.True(registry.TryGetAgentInfo(npcId, out var npcInfo));
            Assert.Equal("B", npcInfo.CurrentAuthority);
            Assert.True(registry.TryGetAgentInfo(horseId, out var horseInfo));
            Assert.Equal("B", horseInfo.CurrentAuthority);

            GC.KeepAlive(controller);
        });
    }

    /// <summary>On a host retreat the retreater's own-party cavalry withdraws horse-and-all, while the NPC
    /// cavalry it ran keeps its horses registered for the successor's adoption.</summary>
    [Fact]
    public void HostRetreat_FadesTheRetreatersHorse_AndAdoptsTheNpcsHorse()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var successor = Clients.Skip(1).First(); // "B"

        var ownTroopId = Guid.NewGuid();
        var ownHorseId = Guid.NewGuid();
        var npcTroopId = Guid.NewGuid();
        var npcHorseId = Guid.NewGuid();

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

                Assert.True(successor.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var hostParty));
                var character = (CharacterObject)Game.Current.PlayerTroop;
                var team = new MockTeam(BattleSideEnum.Defender);

                // A's OWN-party cavalry — rider and horse must withdraw together on A's retreat.
                var ownOrigin = new CoopAgentOrigin(character, hostParty.Party, -1, null, new UniqueTroopDescriptor(1));
                var ownTroop = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell).TroopOrigin(ownOrigin));
                var ownHorse = mock.SpawnMount(ownTroop);
                Assert.True(registry.TryRegisterAgent("A", ownTroopId, ownTroop));
                Assert.True(registry.TryRegisterAgent("A", ownHorseId, ownHorse));

                // NPC cavalry A was RUNNING — rider and horse must both be adopted and stay in the field.
                var npcTroop = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
                var npcHorse = mock.SpawnMount(npcTroop);
                Assert.True(registry.TryRegisterAgent("A", npcTroopId, npcTroop));
                Assert.True(registry.TryRegisterAgent("A", npcHorseId, npcHorse));

                successor.Resolve<IMessageBroker>().Publish(this, new MissionPeerLeft("A", mapEventId));
                successor.Resolve<IMessageBroker>().Publish(this, new BattleHostMigrated(mapEventId, "A"));

                // The retreater's cavalry withdrew whole: rider AND horse faded out and deregistered.
                Assert.False(registry.TryGetAgentInfo(ownTroopId, out _));
                Assert.False(registry.TryGetAgentInfo(ownHorseId, out _));
                Assert.True(AgentMirror.TryGet(ownHorse, out var ownHorseMirror));
                Assert.False(ownHorseMirror.IsActive);

                // The NPC cavalry fights on: rider adopted as AI, horse adopted (authority only) and alive.
                Assert.True(registry.TryGetAgentInfo(npcTroopId, out var npcInfo));
                Assert.Equal("B", npcInfo.CurrentAuthority);
                Assert.True(registry.TryGetAgentInfo(npcHorseId, out var npcHorseInfo));
                Assert.Equal("B", npcHorseInfo.CurrentAuthority);
                Assert.True(AgentMirror.TryGet(npcHorse, out var npcHorseMirror));
                Assert.True(npcHorseMirror.IsActive);
                Assert.Equal(AgentControllerType.None, npcHorseMirror.Controller);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>A NON-host retreats while a LIVE rider of another owner sits on one of its registered horses
    /// (e.g. a player who took a loose horse of the retreater's dead cavalry). The horse must not fade —
    /// someone is riding it — nor fall out of the registry: it transfers to the rider's owner, so horse-keyed
    /// routing and death sync keep answering. The retreater's own cavalry still withdraws whole.</summary>
    [Fact]
    public void NonHostRetreat_TransfersItsForeignRiddenHorse_ToTheRidersOwner()
    {
        using var fixture = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, "B");

        var ownTroopId = Guid.NewGuid();
        var ownHorseId = Guid.NewGuid();
        var riderId = Guid.NewGuid();
        var takenHorseId = Guid.NewGuid();

        try
        {
            observer.Call(() =>
            {
                var mock = fixture.CreateMission(observer);
                var controller = observer.Resolve<CoopBattleController>();
                var registry = observer.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin("mapEvent1");
                BattleSpawnGate.BeginBattle("mapEvent1", 1000);

                // The non-host retreat despawn selects A's troops by the player team's side.
                mock.PlayerTeam = mock.DefenderTeam;

                BasicCharacterObject character = Game.Current.PlayerTroop;

                // A's own cavalry — rider and horse withdraw together on the retreat.
                var ownTroop = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(mock.DefenderTeam.Shell));
                var ownHorse = mock.SpawnMount(ownTroop);
                Assert.True(registry.TryRegisterAgent("A", ownTroopId, ownTroop));
                Assert.True(registry.TryRegisterAgent("A", ownHorseId, ownHorse));

                // OUR rider took one of A's loose horses and is still on it when A leaves.
                var ourRider = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.Player).Team(mock.DefenderTeam.Shell));
                var takenHorse = mock.SpawnMount(ourRider);
                Assert.True(registry.TryRegisterAgent("B", riderId, ourRider));
                Assert.True(registry.TryRegisterAgent("A", takenHorseId, takenHorse));

                // A retreats gracefully; no host assignment names it, so it is a NON-host leave.
                observer.Resolve<IMessageBroker>().Publish(this, new MissionPeerLeft("A", "mapEvent1"));

                // A's own cavalry withdrew whole: rider and horse faded out and deregistered.
                Assert.False(registry.TryGetAgentInfo(ownTroopId, out _));
                Assert.False(registry.TryGetAgentInfo(ownHorseId, out _));
                Assert.True(AgentMirror.TryGet(ownHorse, out var ownHorseMirror));
                Assert.False(ownHorseMirror.IsActive);

                // The horse under OUR rider stayed registered and alive — re-keyed to us, not dropped.
                Assert.True(registry.TryGetAgentInfo(takenHorseId, out var takenInfo));
                Assert.Equal("B", takenInfo.CurrentAuthority);
                Assert.True(AgentMirror.TryGet(takenHorse, out var takenHorseMirror));
                Assert.True(takenHorseMirror.IsActive);
                Assert.True(AgentMirror.TryGet(ourRider, out var riderMirror));
                Assert.Same(takenHorse, riderMirror.MountAgent);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }
}
