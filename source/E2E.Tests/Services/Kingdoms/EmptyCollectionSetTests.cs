using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using Common.Serialization;
using Common.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Kingdoms;

/// <summary>
/// E2E tests for AutoSync collection set messages that carry an empty list. Protobuf sends an
/// empty list as null, so each message is sent through the real serializer before the client gets it.
/// </summary>
public class EmptyCollectionSetTests : SyncTestBase
{
    public EmptyCollectionSetTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ServerEmptiesKingdomList_ClientListIsEmpty()
    {
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var setIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._clans)));

        AssertEmptySetReachesClients<Kingdom>(
            kingdomId,
            "Kingdom__clans_SetNetworkMessage",
            kingdom => setIntercept.Invoke(null, new object[] { kingdom, new MBList<Clan>() }),
            (client, kingdom) =>
            {
                Assert.True(client.ObjectManager.TryGetObject(clanId, out Clan clan));
                kingdom._clans = new MBList<Clan> { clan };
            },
            kingdom => kingdom._clans.Count);
    }

    [Fact]
    public void ServerEmptiesBuildQueue_ClientQueueIsEmpty()
    {
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var buildingId = TestEnvironment.CreateRegisteredObject<Building>();
        var setIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Town), nameof(Town.BuildingsInProgress)));

        AssertEmptySetReachesClients<Town>(
            townId,
            "Town_BuildingsInProgress_SetNetworkMessage",
            town => setIntercept.Invoke(null, new object[] { town, new Queue<Building>() }),
            (client, town) =>
            {
                Assert.True(client.ObjectManager.TryGetObject(buildingId, out Building building));
                town.BuildingsInProgress = new Queue<Building>(new[] { building });
            },
            town => town.BuildingsInProgress.Count);
    }

    [Fact]
    public void ServerEmptiesWorkshops_ClientArrayIsEmpty()
    {
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var workshopId = TestEnvironment.CreateRegisteredObject<Workshop>();

        AssertEmptySetReachesClients<Town>(
            townId,
            "Town_Workshops_SetNetworkMessage",
            town => town.Workshops = Array.Empty<Workshop>(),
            (client, town) =>
            {
                Assert.True(client.ObjectManager.TryGetObject(workshopId, out Workshop workshop));
                town.Workshops = new[] { workshop };
            },
            town => town.Workshops.Length);
    }

    private void AssertEmptySetReachesClients<TInstance>(
        string instanceId,
        string messageName,
        Action<TInstance> emptyOnServer,
        Action<EnvironmentInstance, TInstance> seedOnClient,
        Func<TInstance, int> count) where TInstance : class
    {
        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(instanceId, out TInstance serverInstance));
            emptyOnServer(serverInstance);
            Assert.Equal(0, count(serverInstance));
        });

        var message = Server.NetworkSentMessages.Single(sent => sent.GetType().Name == messageName);
        var bytes = Server.Resolve<ICommonSerializer>().Serialize(message);

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(instanceId, out TInstance clientInstance));

            // Give the client a stale entry so the test shows the set really empties it
            client.Call(() =>
            {
                using var setup = new AllowedThread();
                seedOnClient(client, clientInstance);
            });
            Assert.Equal(1, count(clientInstance));

            client.SimulateNetworkPayload(Server.NetPeer, bytes, markGameThread: false);
            client.PumpGameThread();

            client.Call(() => Assert.Equal(0, count(clientInstance)));
        }
    }
}
