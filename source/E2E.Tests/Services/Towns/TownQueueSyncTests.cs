using E2E.Tests.Util;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Towns;

/// <summary>
/// E2E tests for dynamic sync of <see cref="Queue{T}"/> members, using
/// <see cref="Town.BuildingsInProgress"/> (a <c>Queue&lt;Building&gt;</c>) as the synced member.
/// </summary>
public class TownQueueSyncTests : SyncTestBase
{
    readonly string townId;

    public TownQueueSyncTests(ITestOutputHelper output) : base(output)
    {
        townId = TestEnvironment.CreateRegisteredObject<Town>();
    }

    [Fact]
    public void Server_Queue_SetAddRemove_Syncs()
    {
        TestEnvironment.AssertQueueReferenceField<Town, Building>(nameof(Town.BuildingsInProgress), townId);
    }

    [Fact]
    public void Server_Queue_Clear_Syncs()
    {
        // Arrange — seed the queue with two buildings through the synced set/add intercepts
        // so the server and all clients agree on the starting state.
        var fieldInfo = AccessTools.Field(typeof(Town), nameof(Town.BuildingsInProgress));
        var setIntercept = TestEnvironment.GetIntercept(fieldInfo);
        var addIntercept = TestEnvironment.GetCollectionAddIntercept(fieldInfo);

        string firstBuildingId = TestEnvironment.CreateRegisteredObject<Building>();
        string secondBuildingId = TestEnvironment.CreateRegisteredObject<Building>();

        Assert.True(Server.ObjectManager.TryGetObject(firstBuildingId, out Building firstBuilding));

        var queue = new Queue<Building>(new[] { firstBuilding });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(townId, out Town serverTown));

            setIntercept.Invoke(null, new object[] { serverTown, queue });

            Assert.Single(serverTown.BuildingsInProgress);
        });

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(townId, out Town clientTown));
            Assert.Single(clientTown.BuildingsInProgress);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(townId, out Town serverTown));
            Assert.True(Server.ObjectManager.TryGetObject(secondBuildingId, out Building secondBuilding));

            addIntercept.Invoke(null, new object[] { serverTown.BuildingsInProgress, secondBuilding, serverTown });

            Assert.Equal(2, serverTown.BuildingsInProgress.Count);
        });

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(townId, out Town clientTown));
            Assert.True(Server.ObjectManager.TryGetObject(townId, out Town serverTown));
            Assert.True(2 == clientTown.BuildingsInProgress.Count,
                $"client count={clientTown.BuildingsInProgress.Count}, server count={serverTown.BuildingsInProgress.Count}, " +
                $"sameInstance={ReferenceEquals(serverTown, clientTown)}, sameQueue={ReferenceEquals(serverTown.BuildingsInProgress, clientTown.BuildingsInProgress)}");
        }

        // Act — clear the queue on the server through the generated queue clear intercept,
        // the same intercept the dynamic sync transpiler routes Queue<T>.Clear() calls into.
        var patchesType = DynamicSyncPatcher.Assembly.GetType($"DynamicSync.{nameof(Town)}_DynamicPatches");
        Assert.True(patchesType != null, "Generated Town_DynamicPatches type not found in dynamic sync assembly");

        var clearIntercept = patchesType.GetMethod($"Intercept_QueueClear_{nameof(Town.BuildingsInProgress)}");
        Assert.True(clearIntercept != null, "Generated queue clear intercept for Town.BuildingsInProgress not found");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(townId, out Town serverTown));

            clearIntercept.Invoke(null, new object[] { serverTown, serverTown.BuildingsInProgress });

            Assert.Empty(serverTown.BuildingsInProgress);
        });

        // Assert — every client's queue was cleared as well.
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(townId, out Town clientTown));
            Assert.Empty(clientTown.BuildingsInProgress);
        }
    }
}
