using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

public sealed class SettlementOwnershipPreviousOwnerTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;

    public SettlementOwnershipPreviousOwnerTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output, numClients: 1);
    }

    public void Dispose()
    {
        testEnvironment.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SiegeOwnershipMessage_LocalPreviousOwnerMissingOrStale_DispatchesAuthoritativePreviousOwner(
        bool localPreviousOwnerIsStale)
    {
        var settlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        var townId = testEnvironment.CreateRegisteredObject<Town>();
        var newOwnerId = testEnvironment.CreateRegisteredObject<Hero>();
        var previousOwnerId = testEnvironment.CreateRegisteredObject<Hero>();
        var newOwnerClanId = testEnvironment.CreateRegisteredObject<Clan>();
        var localPreviousOwnerId = testEnvironment.CreateRegisteredObject<Hero>();
        var localPreviousOwnerClanId = testEnvironment.CreateRegisteredObject<Clan>();
        var capturerId = testEnvironment.CreateRegisteredObject<Hero>();
        var client = testEnvironment.Clients.Single();
        var receiver = new PreviousOwnerReceiver();
        Hero expectedPreviousOwner = null;

        client.Call(() =>
        {
            var settlement = client.GetRegisteredObject<Settlement>(settlementId);
            var town = client.GetRegisteredObject<Town>(townId);
            var newOwner = client.GetRegisteredObject<Hero>(newOwnerId);
            expectedPreviousOwner = client.GetRegisteredObject<Hero>(previousOwnerId);
            var newOwnerClan = client.GetRegisteredObject<Clan>(newOwnerClanId);
            var localPreviousOwner = client.GetRegisteredObject<Hero>(localPreviousOwnerId);
            var localPreviousOwnerClan = client.GetRegisteredObject<Clan>(localPreviousOwnerClanId);
            using (new AllowedThread())
            {
                newOwnerClan.InitMembers();
                localPreviousOwnerClan.InitMembers();
                settlement.SetSettlementComponent(town);
                settlement.Town.OwnerClan = null;
                newOwner.Clan = newOwnerClan;
                newOwnerClan.SetLeader(newOwner);
                localPreviousOwner.Clan = localPreviousOwnerClan;
                localPreviousOwnerClan.SetLeader(localPreviousOwner);

                if (localPreviousOwnerIsStale)
                {
                    settlement.Town.OwnerClan = localPreviousOwnerClan;
                }
            }
            Assert.NotNull(newOwnerClan._fiefsCache);
            Assert.NotNull(newOwnerClan._settlementsCache);
            Assert.NotNull(settlement.BoundVillages);
            Assert.NotNull(settlement.Party);
            Assert.Same(settlement, settlement.Party.Settlement);
            Assert.Same(settlement, town.Settlement);
            Assert.Same(
                localPreviousOwnerIsStale ? localPreviousOwner : null,
                settlement.OwnerClan?.Leader);
            Campaign.Current.CampaignEventDispatcher.AddCampaignEventReceiver(receiver);
        });

        client.SimulateMessage(
            this,
            new NetworkChangeSettlementOwnership(
                settlementId,
                newOwnerId,
                previousOwnerId,
                capturerId,
                (int)ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege));

        Assert.Same(expectedPreviousOwner, receiver.PreviousOwner);
    }

    private sealed class PreviousOwnerReceiver : CampaignEventReceiver
    {
        public Hero? PreviousOwner { get; private set; }

        public override void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            PreviousOwner = oldOwner;
            _ = oldOwner.Clan;
        }
    }
}
