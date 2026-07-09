using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TownMarketDatas;

/// <summary>
/// E2E tests for dynamic sync of <see cref="Dictionary{TKey, TValue}"/> members, using
/// <see cref="TownMarketData._itemDict"/> (a <c>Dictionary&lt;ItemCategory, ItemData&gt;</c>:
/// reference-synced keys, surrogate-serialized struct values) as the synced member.
/// </summary>
public class TownMarketDataDictionarySyncTests : SyncTestBase
{
    const string MarketDataId = "e2e_town_market_data";

    readonly string categoryId;

    public TownMarketDataDictionarySyncTests(ITestOutputHelper output) : base(output)
    {
        // TownMarketData has no constructor-based registration, so register an instance manually
        // on the server and every client under the same id and seed each with an empty dictionary.
        var serverMarketData = Server.CreateRegisteredObject<TownMarketData>(MarketDataId);
        serverMarketData._itemDict = new Dictionary<ItemCategory, ItemData>();

        foreach (var client in Clients)
        {
            var clientMarketData = client.CreateRegisteredObject<TownMarketData>(MarketDataId);
            clientMarketData._itemDict = new Dictionary<ItemCategory, ItemData>();
        }

        categoryId = TestEnvironment.CreateRegisteredObject<ItemCategory>();
    }

    void AssertClientsHave(string itemCategoryId, ItemData expected)
    {
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(MarketDataId, out TownMarketData clientMarketData));
            Assert.True(client.ObjectManager.TryGetObject(itemCategoryId, out ItemCategory clientCategory));

            Assert.True(clientMarketData._itemDict.TryGetValue(clientCategory, out var clientItemData),
                $"Client dictionary is missing the entry for {itemCategoryId}");
            Assert.Equal(expected.Supply, clientItemData.Supply);
            Assert.Equal(expected.Demand, clientItemData.Demand);
            Assert.Equal(expected.InStore, clientItemData.InStore);
            Assert.Equal(expected.InStoreValue, clientItemData.InStoreValue);
        }
    }

    void AssertClientCounts(int expectedCount)
    {
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(MarketDataId, out TownMarketData clientMarketData));
            Assert.Equal(expectedCount, clientMarketData._itemDict.Count);
        }
    }

    [Fact]
    public void Server_Dictionary_SetItemData_GameMethod_Syncs()
    {
        // Act — call the real game method; the dynamic sync transpiler rewrote its
        // 'this._itemDict[itemCategory] = itemData' indexer call into the set item intercept.
        var itemData = new ItemData(1.5f, 2.5f, 3, 4);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(MarketDataId, out TownMarketData serverMarketData));
            Assert.True(Server.ObjectManager.TryGetObject(categoryId, out ItemCategory serverCategory));

            serverMarketData.SetItemData(serverCategory, itemData);

            Assert.Single(serverMarketData._itemDict);
        });

        // Assert
        AssertClientCounts(1);
        AssertClientsHave(categoryId, itemData);
    }

    [Fact]
    public void Server_Dictionary_Set_Syncs()
    {
        // Act — replace the whole dictionary on the server through the generated field set intercept.
        var fieldInfo = AccessTools.Field(typeof(TownMarketData), nameof(TownMarketData._itemDict));
        var setIntercept = TestEnvironment.GetIntercept(fieldInfo);

        var itemData = new ItemData(10f, 20f, 30, 40);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(MarketDataId, out TownMarketData serverMarketData));
            Assert.True(Server.ObjectManager.TryGetObject(categoryId, out ItemCategory serverCategory));

            var newDictionary = new Dictionary<ItemCategory, ItemData>
            {
                [serverCategory] = itemData,
            };

            setIntercept.Invoke(null, new object[] { serverMarketData, newDictionary });

            Assert.Single(serverMarketData._itemDict);
        });

        // Assert
        AssertClientCounts(1);
        AssertClientsHave(categoryId, itemData);
    }

    [Fact]
    public void Server_Dictionary_Add_Syncs()
    {
        // Act — add an entry on the server through the generated dictionary add intercept.
        var fieldInfo = AccessTools.Field(typeof(TownMarketData), nameof(TownMarketData._itemDict));
        var addIntercept = TestEnvironment.GetDictionaryAddIntercept(fieldInfo);

        var itemData = new ItemData(5f, 6f, 7, 8);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(MarketDataId, out TownMarketData serverMarketData));
            Assert.True(Server.ObjectManager.TryGetObject(categoryId, out ItemCategory serverCategory));

            addIntercept.Invoke(null, new object[] { serverMarketData, serverMarketData._itemDict, serverCategory, itemData });

            Assert.Single(serverMarketData._itemDict);
        });

        // Assert
        AssertClientCounts(1);
        AssertClientsHave(categoryId, itemData);
    }

    [Fact]
    public void Server_Dictionary_Remove_Syncs()
    {
        // Arrange — seed one entry through the synced game method so all instances agree.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(MarketDataId, out TownMarketData serverMarketData));
            Assert.True(Server.ObjectManager.TryGetObject(categoryId, out ItemCategory serverCategory));

            serverMarketData.SetItemData(serverCategory, new ItemData(1f, 2f, 3, 4));
        });

        AssertClientCounts(1);

        // Act — remove the entry on the server through the generated dictionary remove intercept.
        var fieldInfo = AccessTools.Field(typeof(TownMarketData), nameof(TownMarketData._itemDict));
        var removeIntercept = TestEnvironment.GetDictionaryRemoveIntercept(fieldInfo);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(MarketDataId, out TownMarketData serverMarketData));
            Assert.True(Server.ObjectManager.TryGetObject(categoryId, out ItemCategory serverCategory));

            var removed = removeIntercept.Invoke(null, new object[] { serverMarketData, serverMarketData._itemDict, serverCategory });

            Assert.True((bool)removed);
            Assert.Empty(serverMarketData._itemDict);
        });

        // Assert
        AssertClientCounts(0);
    }

    [Fact]
    public void Server_Dictionary_Clear_Syncs()
    {
        // Arrange — seed two entries through the synced game method so all instances agree.
        var secondCategoryId = TestEnvironment.CreateRegisteredObject<ItemCategory>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(MarketDataId, out TownMarketData serverMarketData));
            Assert.True(Server.ObjectManager.TryGetObject(categoryId, out ItemCategory serverCategory));
            Assert.True(Server.ObjectManager.TryGetObject(secondCategoryId, out ItemCategory secondServerCategory));

            serverMarketData.SetItemData(serverCategory, new ItemData(1f, 2f, 3, 4));
            serverMarketData.SetItemData(secondServerCategory, new ItemData(5f, 6f, 7, 8));

            Assert.Equal(2, serverMarketData._itemDict.Count);
        });

        AssertClientCounts(2);

        // Act — clear the dictionary on the server through the generated dictionary clear intercept.
        var fieldInfo = AccessTools.Field(typeof(TownMarketData), nameof(TownMarketData._itemDict));
        var clearIntercept = TestEnvironment.GetDictionaryClearIntercept(fieldInfo);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(MarketDataId, out TownMarketData serverMarketData));

            clearIntercept.Invoke(null, new object[] { serverMarketData, serverMarketData._itemDict });

            Assert.Empty(serverMarketData._itemDict);
        });

        // Assert
        AssertClientCounts(0);
    }
}
