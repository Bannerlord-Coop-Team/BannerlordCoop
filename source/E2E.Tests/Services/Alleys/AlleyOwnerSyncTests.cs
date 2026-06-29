using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Alleys;

/// <summary>
/// Verifies that an alley owner change driven on the server replicates to every client by
/// replaying <c>Alley.SetOwner</c>, so the owner, the derived <c>State</c> and the owner's
/// <c>OwnedAlleys</c> list all stay consistent (rather than just poking the owner field).
/// </summary>
public class AlleyOwnerSyncTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }

    public AlleyOwnerSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerSetOwner_ReplicatesOwnerStateAndOwnedAlleys()
    {
        var server = TestEnvironment.Server;
        string alleyId = null;
        string heroId = null;

        server.Call(() =>
        {
            var alley = GameObjectCreator.CreateInitializedObject<Alley>();
            Assert.True(server.ObjectManager.TryGetId(alley, out alleyId));
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            Assert.True(server.ObjectManager.TryGetId(hero, out heroId));

            alley.SetOwner(hero);
            Assert.Equal(hero, alley.Owner);
            Assert.Contains(alley, hero.OwnedAlleys);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Alley>(alleyId, out var alley));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            Assert.Equal(hero, alley.Owner);
            // The synced owner is not this client's main hero, so the alley reads as gang-controlled.
            Assert.Equal(Alley.AreaState.OccupiedByGangLeader, alley.State);
            Assert.Contains(alley, hero.OwnedAlleys);
        }
    }

    [Fact]
    public void ServerChangeOwner_MovesAlleyBetweenOwnedAlleys()
    {
        var server = TestEnvironment.Server;
        string alleyId = null;
        string heroAId = null;
        string heroBId = null;

        server.Call(() =>
        {
            var alley = GameObjectCreator.CreateInitializedObject<Alley>();
            Assert.True(server.ObjectManager.TryGetId(alley, out alleyId));
            var heroA = GameObjectCreator.CreateInitializedObject<Hero>();
            Assert.True(server.ObjectManager.TryGetId(heroA, out heroAId));
            var heroB = GameObjectCreator.CreateInitializedObject<Hero>();
            Assert.True(server.ObjectManager.TryGetId(heroB, out heroBId));

            alley.SetOwner(heroA);
            alley.SetOwner(heroB);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Alley>(alleyId, out var alley));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroAId, out var heroA));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroBId, out var heroB));

            Assert.Equal(heroB, alley.Owner);
            Assert.DoesNotContain(alley, heroA.OwnedAlleys);
            Assert.Contains(alley, heroB.OwnedAlleys);
        }
    }

    [Fact]
    public void ServerClearOwner_ReplicatesEmptyState()
    {
        var server = TestEnvironment.Server;
        string alleyId = null;
        string heroId = null;

        server.Call(() =>
        {
            var alley = GameObjectCreator.CreateInitializedObject<Alley>();
            Assert.True(server.ObjectManager.TryGetId(alley, out alleyId));
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            Assert.True(server.ObjectManager.TryGetId(hero, out heroId));

            alley.SetOwner(hero);
            alley.SetOwner(null);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Alley>(alleyId, out var alley));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            Assert.Null(alley.Owner);
            Assert.Equal(Alley.AreaState.Empty, alley.State);
            Assert.DoesNotContain(alley, hero.OwnedAlleys);
        }
    }
}
