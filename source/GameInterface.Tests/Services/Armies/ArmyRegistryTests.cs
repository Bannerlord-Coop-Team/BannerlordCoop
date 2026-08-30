using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Armies;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Armies;

[Collection("CampaignCurrentCollection")]
public class ArmyRegistryTests
{
    [Fact]
    public void RegisterAllObjects_TwoArmiesInOneKingdomUseMatchingLeaderPartyIds()
    {
        var serverObjectManager = new ObjectManager(Mock.Of<ILogger>());
        var clientObjectManager = new ObjectManager(Mock.Of<ILogger>());
        var serverArmies = new[]
        {
            CreateArmy("lord_1_1_party_1"),
            CreateArmy("lord_1_2_party_1"),
        };
        var clientArmies = new[]
        {
            CreateArmy("lord_1_1_party_1"),
            CreateArmy("lord_1_2_party_1"),
        };

        RunWithArmies(serverArmies, () => CreateRegistry(serverObjectManager).RegisterAllObjects());
        RunWithArmies(clientArmies, () => CreateRegistry(clientObjectManager).RegisterAllObjects());

        Assert.True(serverObjectManager.TryGetId(serverArmies[0], out var firstServerId));
        Assert.True(serverObjectManager.TryGetId(serverArmies[1], out var secondServerId));
        Assert.True(clientObjectManager.TryGetId(clientArmies[0], out var firstClientId));
        Assert.True(clientObjectManager.TryGetId(clientArmies[1], out var secondClientId));
        Assert.Equal("Army_lord_1_1_party_1", firstServerId);
        Assert.Equal("Army_lord_1_2_party_1", secondServerId);
        Assert.Equal(firstServerId, firstClientId);
        Assert.Equal(secondServerId, secondClientId);
        Assert.NotEqual(firstServerId, secondServerId);
    }

    [Fact]
    public void RegisterAllObjectsWithRemap_RepeatedRefreshRetainsServerId()
    {
        const string derivedId = "Army_lord_1_3_party_1";
        const string serverId = "Army_Created_42";
        var serverArmy = CreateArmy("lord_1_3_party_1");
        var serverObjectManager = new ObjectManager(Mock.Of<ILogger>());
        Assert.True(serverObjectManager.AddExisting(serverId, serverArmy));
        var serverRegistry = CreateRegistry(serverObjectManager);
        var firstRemap = new Dictionary<string, string>();
        var secondRemap = new Dictionary<string, string>();

        RunWithArmies(serverArmy, () =>
        {
            serverRegistry.CollectIdRemap(firstRemap);
            serverRegistry.CollectIdRemap(secondRemap);
        });

        Assert.Equal(serverId, firstRemap[derivedId]);
        Assert.Equal(serverId, secondRemap[derivedId]);

        var clientArmy = CreateArmy("lord_1_3_party_1");
        var clientObjectManager = new ObjectManager(Mock.Of<ILogger>());
        var clientRegistry = CreateRegistry(clientObjectManager);

        RunWithArmies(clientArmy, () =>
        {
            clientRegistry.RegisterAllObjectsWithRemap(firstRemap);
            clientRegistry.RegisterAllObjects();
            clientRegistry.RegisterAllObjectsWithRemap(secondRemap);
        });

        Assert.True(clientObjectManager.TryGetId(clientArmy, out var clientId));
        Assert.Equal(serverId, clientId);
        Assert.True(clientObjectManager.TryGetObject<Army>(serverId, out var registeredArmy));
        Assert.Same(clientArmy, registeredArmy);
        Assert.False(clientObjectManager.TryGetObject<Army>(derivedId, out _));
    }

    private static ArmyRegistry CreateRegistry(IObjectManager objectManager)
    {
        return new ArmyRegistry(
            Mock.Of<ILogger>(),
            Mock.Of<IAutoRegistryFactory>(),
            objectManager);
    }

    private static Army CreateArmy(string leaderPartyId)
    {
        var leaderParty = ObjectHelper.SkipConstructor<MobileParty>();
        leaderParty.StringId = leaderPartyId;
        var army = ObjectHelper.SkipConstructor<Army>();
        army.LeaderParty = leaderParty;
        return army;
    }

    private static void RunWithArmies(Army army, Action action)
    {
        RunWithArmies(new[] { army }, action);
    }

    private static void RunWithArmies(IEnumerable<Army> armies, Action action)
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
            kingdom._armies = new MBList<Army>();
            foreach (var army in armies)
            {
                kingdom._armies.Add(army);
                army._kingdom = kingdom;
            }

            var campaignObjectManager = new CampaignObjectManager();
            campaignObjectManager._kingdoms.Add(kingdom);
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            campaign.CampaignObjectManager = campaignObjectManager;
            Campaign.Current = campaign;

            action();
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }
}
