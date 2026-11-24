using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.AnchorPoints;

/// <summary>
/// A player sailing into a town: the fleet is anchored there before the client opens the town menu that reads it.
/// </summary>
public class SailIntoPortEntryTests : MapEventTestBase
{
    private static readonly CampaignVec2 Gate = new(new Vec2(20f, 30f), true);
    private static readonly CampaignVec2 Port = new(new Vec2(21f, 31f), false);

    // The AutoSync message for a write to AnchorPoint._settlement
    private const string AnchorSettlementSetMessage = "AnchorPoint__settlement_SetNetworkMessage";

    /// <summary>Passes the test output to the environment.</summary>
    public SailIntoPortEntryTests(ITestOutputHelper output) : base(output) { }

    // The approval opens the town menu, whose Set sail option needs the fleet docked there (#4881)
    [Fact]
    public void SettlementEncounterRequest_FromTheSea_AnchorsTheFleetBeforeTheApproval()
    {
        var client = Clients.First();
        var (partyId, settlementId) = CreatePlayerAtSeaNearTown(client);

        // The send order is asserted by index, so only the entry's own sends count
        Server.NetworkSentMessages.Clear();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestStartSettlementEncounter(
            client.GetHandle<MobileParty>(partyId),
            client.GetHandle<Settlement>(settlementId))));

        var sent = Server.NetworkSentMessages.Messages.ToList();
        var approval = Assert.Single(sent, message => message is NetworkStartSettlementEncounter);
        var anchored = Assert.Single(sent, message => message.GetType().Name == AnchorSettlementSetMessage);
        Assert.True(sent.IndexOf(anchored) < sent.IndexOf(approval), "the fleet was anchored after the approval");

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            Assert.Same(settlement, party.CurrentSettlement);
            Assert.True(party.Anchor.IsAtSettlement(settlement));
        });
    }

    // A registered player's party at sea in front of a town's gate, with one ship and no anchor.
    private (string partyId, string settlementId) CreatePlayerAtSeaNearTown(EnvironmentInstance client)
    {
        const string controllerId = "PlayerOne";
        var (_, partyId) = CreatePlayerHeroParty(controllerId);
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var hullId = TestEnvironment.CreateRegisteredObject<ShipHull>();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                // Hull fields and settlement positions are template data every machine loads the same
                var hull = instance.GetRegisteredObject<ShipHull>(hullId);
                hull.MaxHitPoints = 100;
                hull.MaxSailHitPoints = 100;
                hull.AvailableSlots = new Dictionary<string, ShipSlot>().GetReadOnlyDictionary();

                var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
                var town = instance.GetRegisteredObject<Town>(townId);
                using (new AllowedThread())
                {
                    settlement.Town = town;
                    settlement.SetSettlementComponent(town);
                    settlement.Party = new PartyBase(settlement);
                    settlement.GatePosition = Gate;
                    settlement.PortPosition = Port;
                }
            });
        }

        Server.Call(() =>
        {
            EnsureEncounterModel();
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            ChangeShipOwnerAction.ApplyByProduction(party.Party, new Ship(Server.GetRegisteredObject<ShipHull>(hullId)));
            using (new AllowedThread())
            {
                party.Position = Gate;
                party.IsCurrentlyAtSea = true;
                party.IsActive = true;
            }
        });

        return (partyId, settlementId);
    }
}
