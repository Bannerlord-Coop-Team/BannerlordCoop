using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Ships;

[Collection("CampaignCurrentCollection")]
public class ShipRegistryTests
{
    [Fact]
    public void RegisterAllObjects_DerivesIdsFromOwnerAndIndex()
    {
        var objectManager = new ObjectManager(Mock.Of<ILogger>());
        var lordParty = CreateMobileParty("lord_1_1_party_1", out var lordShips, 2);
        var town = CreateSettlement("town_ES1", out var townShips, 1);

        RunWithCampaign(new[] { lordParty }, new[] { town }, () => CreateRegistry(objectManager).RegisterAllObjects());

        Assert.True(objectManager.TryGetId(lordShips[0], out var firstLordShipId));
        Assert.True(objectManager.TryGetId(lordShips[1], out var secondLordShipId));
        Assert.True(objectManager.TryGetId(townShips[0], out var townShipId));
        Assert.Equal("Ship_lord_1_1_party_1_0", firstLordShipId);
        Assert.Equal("Ship_lord_1_1_party_1_1", secondLordShipId);
        Assert.Equal("Ship_town_ES1_0", townShipId);
    }

    [Fact]
    public void RegisterAllObjectsWithRemap_JoiningClientAdoptsServerIdOfLiveCreatedShip()
    {
        const string derivedId = "Ship_lord_1_1_party_1_0";
        const string serverId = "Ship_Created_7";
        var serverObjectManager = new ObjectManager(Mock.Of<ILogger>());
        var serverParty = CreateMobileParty("lord_1_1_party_1", out var serverShips, 1);
        Assert.True(serverObjectManager.AddExisting(serverId, serverShips[0]));
        var remap = new Dictionary<string, string>();

        RunWithCampaign(new[] { serverParty }, Array.Empty<Settlement>(),
            () => CreateRegistry(serverObjectManager).CollectIdRemap(remap));

        Assert.Equal(serverId, remap[derivedId]);

        var clientObjectManager = new ObjectManager(Mock.Of<ILogger>());
        var clientParty = CreateMobileParty("lord_1_1_party_1", out var clientShips, 1);

        RunWithCampaign(new[] { clientParty }, Array.Empty<Settlement>(),
            () => CreateRegistry(clientObjectManager).RegisterAllObjectsWithRemap(remap));

        Assert.True(clientObjectManager.TryGetId(clientShips[0], out var clientId));
        Assert.Equal(serverId, clientId);
        Assert.False(clientObjectManager.TryGetObject<Ship>(derivedId, out _));
    }

    private static ShipRegistry CreateRegistry(IObjectManager objectManager)
    {
        return new ShipRegistry(Mock.Of<ILogger>(), Mock.Of<IAutoRegistryFactory>(), objectManager);
    }

    private static MobileParty CreateMobileParty(string stringId, out MBList<Ship> ships, int shipCount)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.StringId = stringId;
        party.Party = CreatePartyBase(out ships, shipCount);
        return party;
    }

    private static Settlement CreateSettlement(string stringId, out MBList<Ship> ships, int shipCount)
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.StringId = stringId;
        settlement.Party = CreatePartyBase(out ships, shipCount);
        return settlement;
    }

    private static PartyBase CreatePartyBase(out MBList<Ship> ships, int shipCount)
    {
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        ships = new MBList<Ship>();
        for (int i = 0; i < shipCount; i++)
        {
            ships.Add(ObjectHelper.SkipConstructor<Ship>());
        }
        party._ships = ships;
        return party;
    }

    private static void RunWithCampaign(IEnumerable<MobileParty> parties, IEnumerable<Settlement> settlements, Action action)
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var campaignObjectManager = new CampaignObjectManager();
            foreach (var party in parties)
            {
                campaignObjectManager._mobileParties.Add(party);
            }
            campaignObjectManager.Settlements = new MBList<Settlement>(settlements);

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
