using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class MercenaryStockSyncTests : SyncTestBase
{
    private readonly string settlementId;
    private readonly string townId;
    private readonly string townPartyId;
    private readonly string troopId;

    public MercenaryStockSyncTests(ITestOutputHelper output) : base(output)
    {
        settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        townId = TestEnvironment.CreateRegisteredObject<Town>();
        townPartyId = TestEnvironment.CreateRegisteredObject<PartyBase>();
        troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        AddRecruitmentBehavior(Server);
        ConfigureTownSettlement(Server);
        foreach (var client in Clients)
        {
            AddRecruitmentBehavior(client);
            ConfigureTownSettlement(client);
        }
    }

    [Fact]
    public void Server_MercenaryStockChanged_ReplicatesStockToClients()
    {
        Server.Call(() =>
        {
            var town = Server.GetRegisteredObject<Town>(townId);
            var troop = Server.GetRegisteredObject<CharacterObject>(troopId);

            MessageBroker.Instance.Publish(this, new MercenaryStockChanged(town, troop, 12));
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var town = client.GetRegisteredObject<Town>(townId);
                var troop = client.GetRegisteredObject<CharacterObject>(troopId);
                var mercenaryData = Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>().GetMercenaryData(town);

                Assert.True(town.Settlement.IsTown);
                Assert.Same(troop, mercenaryData.TroopType);
                Assert.Equal(12, mercenaryData.Number);
            });
        }
    }

    private void ConfigureTownSettlement(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
            var town = instance.GetRegisteredObject<Town>(townId);
            var townParty = instance.GetRegisteredObject<PartyBase>(townPartyId);

            settlement.Town = town;
            townParty.Settlement = settlement;
            town.Owner = townParty;
        });
    }

    private static void AddRecruitmentBehavior(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            var manager = new CampaignBehaviorManager(new CampaignBehaviorBase[]
            {
                new RecruitmentCampaignBehavior()
            });
            Campaign.Current.AddCampaignBehaviorManager(manager);
        });
    }
}