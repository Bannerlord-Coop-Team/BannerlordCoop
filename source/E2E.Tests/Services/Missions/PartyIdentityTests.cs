using System;
using System.Linq;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using Missions;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-105 (Party Identity): "All battle participants and results shall use stable party, hero, troop, player, and
/// agent identifiers." The identifiers are stable when the id the server assigns resolves to the corresponding
/// object on every client (round-trips there) and back to the same id — so a battle command or result keyed by an
/// id lands on the right object on each instance. This exercises the established identity model directly: the
/// <c>IObjectManager</c> string id for parties/heroes/troop characters, the controller id for players, and the
/// per-agent <see cref="Guid"/> in the <see cref="NetworkAgentRegistry"/>.
/// </summary>
public class PartyIdentityTests : MapEventTestBase
{
    public PartyIdentityTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// A party, its hero, a troop character, and the owning player are created/registered on the server and
    /// replicated to both clients. Each id must resolve to a (distinct, per-client) object on BOTH clients and
    /// round-trip back to the exact same server-assigned id; the player's controller id must resolve on both
    /// clients to a record carrying the same stable hero/party ids. That is the cross-client stability BR-105
    /// requires of battle participant identifiers.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-105")]
    public void PartyHeroTroopAndPlayerIds_ResolveToTheSameObjects_AcrossBothClients()
    {
        const string controllerId = "player1";
        var (heroId, partyId) = CreatePlayerHeroParty(controllerId);
        var troopCharacterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        // Two clients in the environment; each maintains its own mirror of the shared objects under the same ids.
        var clients = Clients.ToArray();
        Assert.Equal(2, clients.Length);

        MobileParty PartyOn(EnvironmentInstance instance)
        {
            MobileParty resolved = null;
            instance.Call(() =>
            {
                // Id -> object resolves on this client.
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out resolved));
                // object -> id round-trips back to the SAME server-assigned id.
                Assert.True(instance.ObjectManager.TryGetId(resolved, out var roundTrip));
                Assert.Equal(partyId, roundTrip);

                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetId(hero, out var heroRoundTrip));
                Assert.Equal(heroId, heroRoundTrip);

                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopCharacterId, out var troop));
                Assert.True(instance.ObjectManager.TryGetId(troop, out var troopRoundTrip));
                Assert.Equal(troopCharacterId, troopRoundTrip);

                // The player id resolves to a record carrying the same stable hero/party ids on this client.
                Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
                Assert.Equal(partyId, player.MobilePartyId);
                Assert.Equal(heroId, player.HeroId);
            });
            return resolved;
        }

        var partyOnA = PartyOn(clients[0]);
        var partyOnB = PartyOn(clients[1]);

        // Each client holds its OWN instance of the party (they are separate processes' mirrors), yet both are
        // addressed by the identical stable id — the essence of a stable cross-client identifier.
        Assert.NotSame(partyOnA, partyOnB);
    }

    /// <summary>
    /// The agent identifier is a <see cref="Guid"/> in the <see cref="NetworkAgentRegistry"/>. It must round-trip:
    /// the stable id resolves back to the exact same <see cref="Agent"/>, and the agent resolves back to the same
    /// id, while preserving the owning player's (stable) controller id — so per-agent battle results attribute to
    /// the right participant.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-105")]
    public void AgentIdentifier_RoundTripsToTheSameAgent_AndPreservesStableOwner()
    {
        var provider = new Mock<IControllerIdProvider>();
        provider.SetupGet(p => p.ControllerId).Returns("me");
        var registry = new NetworkAgentRegistry(provider.Object);

        var agent = ObjectHelper.SkipConstructor<Agent>();
        var agentId = Guid.NewGuid();

        Assert.True(registry.TryRegisterAgent("owner1", agentId, agent));

        // id -> the same agent, carrying the stable owner.
        Assert.True(registry.TryGetAgentInfo(agentId, out var byId));
        Assert.Same(agent, byId.Agent);
        Assert.Equal(agentId, byId.AgentId);
        Assert.Equal("owner1", byId.OriginalOwner);

        // agent -> the same id (the reverse lookup resolves to the identical stable id).
        Assert.True(registry.TryGetAgentInfo(agent, out var byAgent));
        Assert.Equal(agentId, byAgent.AgentId);
    }
}
