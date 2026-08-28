using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.Missions;
using E2E.Tests.Util;
using GameInterface.Services.Locations.Hosting;
using Missions;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>
/// E2E test environment for the settlement-location NPC host stack. Extends
/// <see cref="MissionTestEnvironment"/> (battle host env) so the registry-isolation tests can stand up
/// BOTH a battle and a location with the same controllers; adds helpers to stand up a settlement
/// location instance (a server-side <see cref="Settlement"/> with one player party per controller
/// inside it) and to drive + inspect the server-authoritative location host election.
/// <para>
/// This base remains focused on campaign election state. <see cref="SettlementTestEnvironment"/>
/// composes it with the headless mission engine and P2P mesh for vertical location tests.
/// </para>
/// </summary>
public class LocationHostTestEnvironment : MissionTestEnvironment
{
    public LocationHostTestEnvironment(ITestOutputHelper output, int numClients = 2) : base(output, numClients)
    {
    }

    /// <summary>
    /// Stands up a settlement location instance: a server-side <see cref="Settlement"/>, one player
    /// <see cref="MobileParty"/> per supplied controller id parked inside it (the election's
    /// party-in-settlement validation reads <c>CurrentSettlement</c>), players registered on every
    /// instance, with client <c>i</c> given <c>controllerIds[i]</c>. Returns the derived location
    /// instance id and the per-controller party ids.
    /// </summary>
    protected (string instanceId, string[] partyIds) SetupSettlementLocation(params string[] controllerIds)
    {
        Assert.True(controllerIds.Length >= 1, "Need at least one player for a location instance");

        var clients = Clients.ToArray();
        for (int i = 0; i < controllerIds.Length && i < clients.Length; i++)
            SetControllerId(clients[i], controllerIds[i]);

        var partyIds = new string[controllerIds.Length];

        Server.Call(() =>
        {
            for (int i = 0; i < controllerIds.Length; i++)
            {
                var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
                Assert.True(Server.ObjectManager.TryGetId(party, out partyIds[i]));
            }
        });

        for (int i = 0; i < controllerIds.Length; i++)
        {
            var heroId = CreateRegisteredObject<Hero>();
            string characterId = GetPlayerCharacterId(heroId, $"{controllerIds[i]}Character");
            ConfigurePlayerIdentity(heroId, partyIds[i], characterId);
            RegisterAsPlayerParty(controllerIds[i], heroId, partyIds[i], characterId);
        }

        var instanceId = ParkPartiesInNewSettlement(partyIds);
        return (instanceId, partyIds);
    }

    /// <summary>
    /// Creates a server-side <see cref="Settlement"/> and parks the given (already registered) parties
    /// inside it, returning the derived location instance id. Used directly when the parties already
    /// exist — e.g. the registry-isolation test parks the BATTLE parties in a settlement, since a
    /// party can be in a map event and a settlement at once and a player has exactly one party.
    /// </summary>
    protected string ParkPartiesInNewSettlement(params string[] partyIds)
    {
        string? settlementId = null;

        Server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));

            foreach (var partyId in partyIds)
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

                // The native CurrentSettlement setter maintains settlement party lists and other
                // invariants the headless harness does not need — the election only READS the value, so
                // set the backing field directly.
                party._currentSettlement = settlement;
            }
        });

        Assert.NotNull(settlementId);
        string locationId = CreateRegisteredObject<TaleWorlds.CampaignSystem.Settlements.Locations.Location>();
        return $"{settlementId}|{locationId}";
    }

    private string GetPlayerCharacterId(string heroId, string fallbackId)
    {
        string characterId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.NotNull(hero.CharacterObject);
            if (!Server.ObjectManager.TryGetId(hero.CharacterObject, out characterId))
            {
                Assert.True(Server.ObjectManager.AddExisting(fallbackId, hero.CharacterObject));
                characterId = fallbackId;
            }
        });
        return characterId;
    }

    private void ConfigurePlayerIdentity(
        string heroId,
        string partyId,
        string characterId)
    {
        void Configure(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.NotNull(hero.CharacterObject);

                if (!instance.ObjectManager.TryGetId(hero.CharacterObject, out _))
                    Assert.True(instance.ObjectManager.AddExisting(characterId, hero.CharacterObject));

                using (new Common.Util.AllowedThread())
                {
                    hero.PartyBelongedTo = party;
                }
            });
        }

        Configure(Server);
        foreach (var client in Clients)
            Configure(client);
    }

    /// <summary>
    /// Simulates <paramref name="client"/> finishing its location mission load (MISSION-READY, SR-010):
    /// publishes <see cref="LocationMissionReady"/> — in the live game
    /// <c>CoopLocationsController.TryRegisterLocalAgent</c> does this — which makes its
    /// <c>LocationHostHandler</c> request host election from the server. The round-trip runs
    /// synchronously through the mock network.
    /// </summary>
    protected void MakeLocationMissionReady(EnvironmentInstance client, string instanceId)
    {
        client.Call(() =>
        {
            client.Resolve<IMessageBroker>().Publish(this, new LocationMissionReady(instanceId));
        });
    }

    /// <summary>Asserts no location host assignment exists on <paramref name="instance"/>.</summary>
    protected void AssertNoLocationHost(EnvironmentInstance instance, string instanceId)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<ILocationHostRegistry>();
            Assert.False(registry.TryGet(instanceId, out _),
                $"Expected no location host assignment for {instanceId} on {instance.GetType().Name}");
        });
    }

    /// <summary>Asserts the elected location host and successor order recorded on <paramref name="instance"/>.</summary>
    protected void AssertLocationHost(EnvironmentInstance instance, string instanceId, string expectedHost, params string[] expectedSuccessors)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<ILocationHostRegistry>();
            Assert.True(registry.TryGet(instanceId, out var assignment), $"No location host assignment on {instance.GetType().Name}");
            Assert.Equal(expectedHost, assignment.HostControllerId);
            Assert.Equal(expectedSuccessors, assignment.SuccessorControllerIds.ToArray());
        });
    }

    /// <summary>Asserts whether <paramref name="instance"/> considers itself the location host.</summary>
    protected void AssertIsLocalLocationHost(EnvironmentInstance instance, string instanceId, bool expected)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<ILocationHostRegistry>();
            Assert.Equal(expected, registry.IsHost(instanceId));
        });
    }

    /// <summary>Reads the epoch of the current location host assignment on <paramref name="instance"/> (0 = none).</summary>
    protected int GetLocationEpoch(EnvironmentInstance instance, string instanceId)
    {
        int epoch = 0;
        instance.Call(() =>
        {
            var registry = instance.Resolve<ILocationHostRegistry>();
            if (registry.TryGet(instanceId, out var assignment))
                epoch = assignment.Epoch;
        });
        return epoch;
    }
}
