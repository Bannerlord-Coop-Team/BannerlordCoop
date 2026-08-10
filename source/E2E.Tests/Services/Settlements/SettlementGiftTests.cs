using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

/// <summary>
/// Covers the kingdom screen's "Give Settlement" once it is routed through the server.
/// </summary>
/// <remarks>
/// A client cannot move a settlement itself - ChangeOwnerOfSettlementPatch blocks it - so the gift is
/// forwarded and the server re-derives authority from the requesting peer. Covered here: the forward,
/// the transfer, the relation bonus vanilla pays and co-op used to skip, and the refusal branches,
/// which are the parts a client can actually provoke.
/// </remarks>
public class SettlementGiftTests : MapEventTestBase
{
    private const string GiverControllerId = "Giver";

    public SettlementGiftTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ClientGift_IsForwardedToTheServer_RatherThanAppliedLocally()
    {
        var client = Clients.First();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var newOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(newOwnerId, out var newOwner));

            // What the kingdom screen's gift ends up publishing on the client.
            client.Resolve<IMessageBroker>().Publish(this, new SettlementGiftRequested(settlement, newOwner));
        });

        TestEnvironment.FlushCoalescer();

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestSettlementOwnership>());
        Assert.Equal(settlementId, request.SettlementId);
        Assert.Equal(newOwnerId, request.NewOwnerId);
    }

    [Fact]
    public void ServerGift_FromTheOwner_TransfersOwnershipAndPaysTheRelationBonus()
    {
        var fixture = CreateGiftFixture();

        int relationBefore = 0;
        int expectedBonus = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.GiverHeroId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.ReceiverHeroId, out var receiver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));

            relationBefore = giver.GetRelation(receiver);
            expectedBonus = settlement.IsTown
                ? Campaign.Current.Models.DiplomacyModel.GiftingTownRelationshipBonus
                : Campaign.Current.Models.DiplomacyModel.GiftingCastleRelationshipBonus;
        });

        SendGiftRequest(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.GiverHeroId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.ReceiverHeroId, out var receiver));

            Assert.Same(receiver.Clan, settlement.OwnerClan);

            // The goodwill vanilla pays for a gift, which ApplyByGift on its own does not.
            Assert.NotEqual(0, expectedBonus);
            Assert.Equal(relationBefore + expectedBonus, giver.GetRelation(receiver));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkSettlementGiftRejected>());
    }

    [Fact]
    public void ServerGift_FromSomeoneWhoDoesNotOwnIt_IsRefusedAndTheRequesterIsTold()
    {
        var fixture = CreateGiftFixture();

        // Move the fief away from the requester, leaving every other precondition intact.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.ReceiverHeroId, out var receiver));
            using (new AllowedThread())
            {
                settlement.Town._ownerClan = receiver.Clan;
            }
        });

        SendGiftRequest(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.ReceiverHeroId, out var receiver));
            Assert.Same(receiver.Clan, settlement.OwnerClan);
        });

        // The kingdom screen closes its popup on confirm, so a refusal that only reached the server log
        // would look exactly like the silent no-op this feature set out to fix.
        var rejection = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkSettlementGiftRejected>());
        Assert.False(string.IsNullOrWhiteSpace(rejection.Reason));
    }

    /// <summary>
    /// An owner who does not rule the kingdom is refused: vanilla never offers them the gift popup.
    /// </summary>
    /// <remarks>
    /// The gift is gated TWICE, in sequence, and an earlier version of this handler only mirrored the first:
    ///
    ///   KingdomSettlementVM.ExecuteAnnex  settlement.OwnerClan.Leader == Hero.MainHero -> _onGrantFief
    ///   KingdomManagementVM.OnGrantFief   Kingdom.Leader == Hero.MainHero -> GiftFief.OpenWith(settlement)
    ///
    /// A vassal who owns a fief fails the second gate and is offered "give this settlement back to your
    /// kingdom" - RelinquishSettlementOwnership, which returns it to the realm rather than handing it to a
    /// hero of the giver's choosing. Accepting the request here would have let a client perform a transfer
    /// the game does not offer, and no other test catches it: the fixture's giver rules the kingdom, so
    /// every other case passes under both rules.
    /// </remarks>
    [Fact]
    public void ServerGift_FromAnOwnerWhoDoesNotRuleTheKingdom_IsRefused()
    {
        var fixture = CreateGiftFixture();

        // Hand the throne to the recipient. The requester still OWNS the fief - only rulership moves, so
        // this isolates the second gate from the first.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.ReceiverHeroId, out var receiver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.GiverHeroId, out var giver));

            using (new AllowedThread())
            {
                settlement.OwnerClan.Kingdom._rulingClan = receiver.Clan;
            }

            Assert.Same(giver.Clan, settlement.OwnerClan);
        });

        SendGiftRequest(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.GiverHeroId, out var giver));

            // Refused: the fief has not moved.
            Assert.Same(giver.Clan, settlement.OwnerClan);
        });

        var rejection = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkSettlementGiftRejected>());
        Assert.False(string.IsNullOrWhiteSpace(rejection.Reason));
    }

    [Fact]
    public void ServerGift_RepeatedAfterTheFiefMoved_DoesNotTransferItASecondTime()
    {
        var fixture = CreateGiftFixture();

        SendGiftRequest(fixture);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.ReceiverHeroId, out var receiver));
            Assert.Same(receiver.Clan, settlement.OwnerClan);
        });

        // A duplicate of the same request. The giver no longer owns the fief, so authority no longer
        // holds - the settlement must stay where the first gift put it.
        SendGiftRequest(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.ReceiverHeroId, out var receiver));
            Assert.Same(receiver.Clan, settlement.OwnerClan);
        });

        Assert.NotEmpty(Server.NetworkSentMessages.GetMessages<NetworkSettlementGiftRejected>());
    }

    private void SendGiftRequest(GiftFixture fixture)
    {
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(
                Clients.First().NetPeer,
                new NetworkRequestSettlementOwnership(fixture.SettlementId, fixture.ReceiverHeroId));
        });

        TestEnvironment.FlushCoalescer();
    }

    /// <summary>
    /// A registered player who owns a fief, and a recipient in the same kingdom - the state the kingdom
    /// screen requires before it offers the gift at all.
    /// </summary>
    private GiftFixture CreateGiftFixture()
    {
        var giver = CreatePlayerHeroParty(GiverControllerId);
        Server.Resolve<IPlayerManager>().SetPeer(GiverControllerId, Clients.First().NetPeer);

        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var receiverHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        void Configure(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giver.heroId, out var giverHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(receiverHeroId, out var receiver));
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

                Assert.NotNull(giverHero.Clan);
                Assert.NotNull(receiver.Clan);

                using (new AllowedThread())
                {
                    // Settlement.OwnerClan reads through Settlement.Town, and the transfer writes through
                    // Town.OwnerClan, so the fief needs its town component wired before either works.
                    settlement.Town = town;
                    town._ownerClan = giverHero.Clan;

                    // Each hero must LEAD its clan: authority is "the owning clan's leader is the
                    // requester", and the relation bonus is paid between the two clan leaders.
                    giverHero.Clan._leader = giverHero;
                    receiver.Clan._leader = receiver;

                    // The giver RULES the kingdom both clans belong to. That matters: it is the case
                    // where "owns it" and "rules its realm" disagree, so a rule that accepted either
                    // would let the ruler give away a fief that is not theirs.
                    kingdom._rulingClan = giverHero.Clan;
                    giverHero.Clan.Kingdom = kingdom;
                    receiver.Clan.Kingdom = kingdom;

                    // Town.Settlement resolves through SettlementComponent._owner, and
                    // Clan.OnFortificationRemoved/Added dereference it while updating their caches.
                    town._owner = settlement.Party;

                    // Those same methods walk the clan caches, which are null on a synthetic clan, so
                    // the transfer would NRE part-way through instead of landing.
                    foreach (var clan in new[] { giverHero.Clan, receiver.Clan })
                    {
                        clan._fiefsCache ??= new MBList<Town>();
                        clan._settlementsCache ??= new MBList<Settlement>();
                        clan._villagesCache ??= new MBList<Village>();
                    }

                    if (!giverHero.Clan._fiefsCache.Contains(town)) giverHero.Clan._fiefsCache.Add(town);
                    if (!giverHero.Clan._settlementsCache.Contains(settlement))
                        giverHero.Clan._settlementsCache.Add(settlement);
                }

                Assert.Same(giverHero.Clan, settlement.OwnerClan);
                Assert.Same(giverHero, settlement.OwnerClan.Leader);
            });
        }

        Configure(Server);
        foreach (var client in Clients) Configure(client);

        return new GiftFixture(settlementId, giver.heroId, receiverHeroId);
    }

    private readonly struct GiftFixture
    {
        public readonly string SettlementId;
        public readonly string GiverHeroId;
        public readonly string ReceiverHeroId;

        public GiftFixture(string settlementId, string giverHeroId, string receiverHeroId)
        {
            SettlementId = settlementId;
            GiverHeroId = giverHeroId;
            ReceiverHeroId = receiverHeroId;
        }
    }
}
