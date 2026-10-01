using Common;
using Common.Messaging;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Handlers;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

[Collection(ModInformationRoleCollection.Name)]
public class ClanRequestSenderValidationTests
{
    private const string ActorId = "actor-hero";
    private const string MemberId = "member-hero";
    private const string ClanId = "clan";

    static ClanRequestSenderValidationTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void FinanceRequest_MatchingSender_AppliesChange()
    {
        using var fixture = new HandlerFixture(ActorId);

        fixture.MessageBroker.Publish(fixture.Peer,
            new RequestClanFinanceChange(ActorId, MemberId, ClanId, 100, false));
        FlushGameThread();

        fixture.ClanFinance.Verify(finance =>
            finance.TryChange(fixture.Actor, fixture.Member, fixture.Clan, 100, false));
    }

    [Theory]
    [InlineData("different-hero")]
    [InlineData(null)]
    public void FinanceRequest_InvalidSender_DoesNotApplyChange(string? registeredHeroId)
    {
        using var fixture = new HandlerFixture(registeredHeroId);

        fixture.MessageBroker.Publish(fixture.Peer,
            new RequestClanFinanceChange(ActorId, MemberId, ClanId, 100, false));
        FlushGameThread();

        fixture.ClanFinance.Verify(finance => finance.TryChange(
            It.IsAny<Hero>(), It.IsAny<Hero>(), It.IsAny<Clan>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void MemberLeaveRequest_MatchingSender_AppliesDeparture()
    {
        using var fixture = new HandlerFixture(ActorId);
        fixture.LeaveRules.Setup(rules => rules.CanLeave(fixture.Actor)).Returns(true);

        fixture.MessageBroker.Publish(fixture.Peer, new RequestClanMemberLeave(ActorId, ActorId));
        FlushGameThread();

        fixture.LeaveRules.Verify(rules => rules.TryApply(fixture.Actor, false));
    }

    [Theory]
    [InlineData("different-hero")]
    [InlineData(null)]
    public void MemberLeaveRequest_InvalidSender_DoesNotApplyDeparture(string? registeredHeroId)
    {
        using var fixture = new HandlerFixture(registeredHeroId);

        fixture.MessageBroker.Publish(fixture.Peer, new RequestClanMemberLeave(ActorId, ActorId));
        FlushGameThread();

        fixture.LeaveRules.Verify(rules => rules.TryApply(It.IsAny<Hero>(), It.IsAny<bool>()), Times.Never);
    }

    private static void FlushGameThread() => GameThread.Run(() => { }, blocking: true);

    private sealed class HandlerFixture : System.IDisposable
    {
        private readonly bool wasServer;
        private readonly ClanFinanceHandler financeHandler;
        private readonly ClanMembershipHandler membershipHandler;

        public MessageBroker MessageBroker { get; } = new();
        public TestNetwork Network { get; } = new();
        public Mock<IClanFinance> ClanFinance { get; } = new();
        public Mock<IClanLeaveRules> LeaveRules { get; } = new();
        public Hero Actor { get; } = ObjectHelper.SkipConstructor<Hero>();
        public Hero Member { get; } = ObjectHelper.SkipConstructor<Hero>();
        public Clan Clan { get; } = ObjectHelper.SkipConstructor<Clan>();
        public LiteNetLib.NetPeer Peer { get; }

        public HandlerFixture(string? registeredHeroId)
        {
            wasServer = ModInformation.IsServer;
            ModInformation.IsServer = true;

            Peer = Network.CreatePeer();
            var playerManager = new Mock<IPlayerManager>();
            if (registeredHeroId != null)
            {
                var player = new Player("controller", registeredHeroId, "party", ClanId, "character");
                playerManager.Setup(manager => manager.TryGetPlayer(Peer, out player)).Returns(true);
            }

            var objectManager = new Mock<IObjectManager>();
            Hero actor = Actor;
            Hero member = Member;
            Clan clan = Clan;
            objectManager.Setup(manager => manager.TryGetObjectWithLogging<Hero>(ActorId, out actor)).Returns(true);
            objectManager.Setup(manager => manager.TryGetObjectWithLogging<Hero>(MemberId, out member)).Returns(true);
            objectManager.Setup(manager => manager.TryGetObjectWithLogging<Clan>(ClanId, out clan)).Returns(true);

            financeHandler = new ClanFinanceHandler(
                MessageBroker, objectManager.Object, Network, ClanFinance.Object, playerManager.Object);
            membershipHandler = new ClanMembershipHandler(
                MessageBroker, objectManager.Object, Network, LeaveRules.Object, playerManager.Object);
        }

        public void Dispose()
        {
            membershipHandler.Dispose();
            financeHandler.Dispose();
            Network.Dispose();
            MessageBroker.Dispose();
            ModInformation.IsServer = wasServer;
        }
    }
}
