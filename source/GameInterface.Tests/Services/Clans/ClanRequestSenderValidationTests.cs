using Common;
using Common.Messaging;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Handlers;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.RomanceFlow;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
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

    [Theory]
    [InlineData("different-hero")]
    [InlineData(null)]
    public void DivorceRequest_InvalidSender_DoesNotApplyDivorce(string? registeredHeroId)
    {
        using var fixture = new HandlerFixture(registeredHeroId);

        fixture.MessageBroker.Publish(fixture.Peer, new RequestPlayerDivorce(ActorId, MemberId));
        FlushGameThread();

        bool separated = false;
        string reason = null!;
        fixture.DivorceRules.Verify(rules => rules.TryApply(
            fixture.Actor, fixture.Member, out separated, out reason), Times.Never);
    }

    private static void FlushGameThread() => GameThread.Run(() => { }, blocking: true);

    private sealed class HandlerFixture : System.IDisposable
    {
        private readonly bool wasServer;
        private readonly ClanFinanceHandler financeHandler;
        private readonly ClanMembershipHandler membershipHandler;
        private readonly PlayerDivorceHandler divorceHandler;

        public MessageBroker MessageBroker { get; } = new();
        public TestNetwork Network { get; } = new();
        public Mock<IClanFinance> ClanFinance { get; } = new();
        public Mock<IClanLeaveRules> LeaveRules { get; } = new();
        public Mock<IPlayerDivorceRules> DivorceRules { get; } = new();
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
            playerManager.Setup(manager => manager.IsSenderControllingHero(Peer, ActorId))
                .Returns(registeredHeroId == ActorId);

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
            divorceHandler = new PlayerDivorceHandler(
                MessageBroker, Network, objectManager.Object, playerManager.Object, DivorceRules.Object);
        }

        public void Dispose()
        {
            divorceHandler.Dispose();
            membershipHandler.Dispose();
            financeHandler.Dispose();
            Network.Dispose();
            MessageBroker.Dispose();
            ModInformation.IsServer = wasServer;
        }
    }
}
