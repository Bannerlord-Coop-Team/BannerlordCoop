using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.PacketHandlers;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Server.Services.MobileParties.PacketHandlers;
using Coop.Core.Server.Services.MobileParties.Packets;
using Coop.Tests.Mocks;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Server.Services.MobileParties;

/// <summary>
/// Verifies authentication and validation of client mobile-party behavior requests.
/// </summary>
public sealed class RequestMobilePartyBehaviorPacketHandlerTests : IDisposable
{
    private const string ControllerId = "controller-1";
    private const string FullPartyId = "MobileParty_OwnedParty";
    private const string CompactPartyId = "OwnedParty";

    private readonly TestMessageBroker messageBroker = new TestMessageBroker();
    private readonly Mock<IPacketManager> packetManager = new Mock<IPacketManager>();
    private readonly Mock<ISendCoalescer> coalescer = new Mock<ISendCoalescer>();
    private readonly Mock<INetwork> network = new Mock<INetwork>();
    private readonly Mock<IPlayerManager> playerManager = new Mock<IPlayerManager>();
    private readonly Mock<IMobilePartyBehaviorSnapshot> mobilePartyBehaviorSnapshot = new Mock<IMobilePartyBehaviorSnapshot>();
    private readonly ObjectManager objectManager;
    private readonly RequestMobilePartyBehaviorPacketHandler handler;
    private readonly NetPeer peer;

    public RequestMobilePartyBehaviorPacketHandlerTests()
    {
        objectManager = new ObjectManager(Mock.Of<ILogger>());
        RegisterParty(FullPartyId);
        var targetParty = RegisterParty("MobileParty_TargetParty");
        RegisterParty("MobileParty_OtherParty");
        RegisterMobilePartyBase("PartyBase_TargetPartyBase", targetParty);
        RegisterSettlementPartyBase("PartyBase_TargetSettlementPartyBase");

        peer = new TestNetwork().CreatePeer();
        var player = new Player(ControllerId, string.Empty, FullPartyId, string.Empty, string.Empty);
        playerManager
            .Setup(manager => manager.TryGetPlayer(
                It.Is<NetPeer>(candidate => ReferenceEquals(candidate, peer)),
                out player))
            .Returns(true);
        var authoritativeData = CreateSnapshot();
        mobilePartyBehaviorSnapshot
            .Setup(snapshot => snapshot.TryCreateCurrent(
                It.IsAny<MobileParty>(),
                out authoritativeData))
            .Returns(true);

        handler = new RequestMobilePartyBehaviorPacketHandler(
            packetManager.Object,
            coalescer.Object,
            messageBroker,
            network.Object,
            objectManager,
            mobilePartyBehaviorSnapshot.Object,
            playerManager.Object);
    }

    public void Dispose() => handler.Dispose();

    [Fact]
    public void OwnerRequest_WithCompactPartyId_IsPublished()
    {
        var data = CreateSnapshot();

        handler.HandlePacket(peer, new RequestMobilePartyBehaviorPacket(data));

        var update = Assert.Single(messageBroker.GetMessagesFromType<UpdatePartyBehavior>());
        Assert.Equal(CompactPartyId, update.BehaviorUpdateData.MobilePartyId);
        Assert.Equal(ControllerId, update.BehaviorUpdateData.OriginControllerId);
    }

    [Fact]
    public void OwnerRequest_ValidationAndPublishRunOnGameThread()
    {
        var scheduledObjectManager = new Mock<IObjectManager>();
        var controlledParty = ObjectHelper.SkipConstructor<MobileParty>();
        bool lookupRanOnGameThread = false;
        scheduledObjectManager
            .Setup(manager => manager.TryGetObject(CompactPartyId, out controlledParty))
            .Callback(() => lookupRanOnGameThread = GameThread.Instance.IsGameThread)
            .Returns(true);
        var scheduledMessageBroker = new TestMessageBroker();
        bool publishRanOnGameThread = false;
        scheduledMessageBroker.Subscribe<UpdatePartyBehavior>(
            _ => publishRanOnGameThread = GameThread.Instance.IsGameThread);
        var scheduledHandler = new RequestMobilePartyBehaviorPacketHandler(
            packetManager.Object,
            coalescer.Object,
            scheduledMessageBroker,
            network.Object,
            scheduledObjectManager.Object,
            Mock.Of<IMobilePartyBehaviorSnapshot>(),
            playerManager.Object);

        try
        {
            scheduledHandler.HandlePacket(peer, new RequestMobilePartyBehaviorPacket(CreateSnapshot()));

            Assert.True(lookupRanOnGameThread);
            Assert.True(publishRanOnGameThread);
            Assert.Single(scheduledMessageBroker.GetMessagesFromType<UpdatePartyBehavior>());
        }
        finally
        {
            scheduledHandler.Dispose();
        }
    }

    [Fact]
    public void RequestForAnotherParty_IsRejected()
    {
        var data = CreateSnapshot(mobilePartyId: "OtherParty");

        handler.HandlePacket(peer, new RequestMobilePartyBehaviorPacket(data));

        Assert.Empty(messageBroker.GetMessagesFromType<UpdatePartyBehavior>());
    }

    [Fact]
    public void RequestFromUnmappedPeer_IsRejected()
    {
        var unmappedPeer = new TestNetwork().CreatePeer();

        handler.HandlePacket(unmappedPeer, new RequestMobilePartyBehaviorPacket(CreateSnapshot()));

        Assert.Empty(messageBroker.GetMessagesFromType<UpdatePartyBehavior>());
    }

    [Fact]
    public void OwnerRequest_WithConsistentPartyReferences_IsPublished()
    {
        var data = CreateSnapshot(
            newBehavior: AiBehavior.EngageParty,
            defaultBehavior: AiBehavior.EngageParty,
            hasTarget: true,
            interactablePointId: "TargetPartyBase");
        data.TargetPartyId = "TargetParty";
        data.PartyMoveMode = MoveModeType.Party;
        data.MoveTargetPartyId = "TargetParty";

        handler.HandlePacket(peer, new RequestMobilePartyBehaviorPacket(data));

        Assert.Single(messageBroker.GetMessagesFromType<UpdatePartyBehavior>());
    }

    [Fact]
    public void SpoofedOrigin_IsReplacedWithAuthenticatedController()
    {
        var data = CreateSnapshot();
        data.OriginControllerId = "spoofed-controller";

        handler.HandlePacket(peer, new RequestMobilePartyBehaviorPacket(data));

        var update = Assert.Single(messageBroker.GetMessagesFromType<UpdatePartyBehavior>());
        Assert.Equal(ControllerId, update.BehaviorUpdateData.OriginControllerId);
    }

    [Fact]
    public void ClientForcePosition_IsClearedAndCannotBypassCoalescer()
    {
        var data = CreateSnapshot();
        data.ForcePosition = true;

        handler.HandlePacket(peer, new RequestMobilePartyBehaviorPacket(data));

        var update = Assert.Single(messageBroker.GetMessagesFromType<UpdatePartyBehavior>());
        Assert.False(update.BehaviorUpdateData.ForcePosition);

        var sanitized = update.BehaviorUpdateData;
        messageBroker.Publish(this, new PartyBehaviorUpdated(ref sanitized));

        coalescer.Verify(
            value => value.Enqueue(
                It.Is<CoalesceKey>(key => key.InstanceId == CompactPartyId),
                It.IsAny<ICoalescedPayload>()),
            Times.Once());
        coalescer.Verify(
            value => value.FlushInstance(It.IsAny<string>(), It.IsAny<INetwork>()),
            Times.Never());
        network.Verify(value => value.SendAll(It.IsAny<IMessage>()), Times.Never());
    }

    [Theory]
    [InlineData(AiBehavior.GoToSettlement)]
    [InlineData(AiBehavior.RaidSettlement)]
    [InlineData(AiBehavior.AssaultSettlement)]
    [InlineData(AiBehavior.BesiegeSettlement)]
    public void SettlementShortTermBehaviorWithoutPartyBase_IsRejected(AiBehavior behavior)
    {
        var data = CreateSnapshot(newBehavior: behavior);

        handler.HandlePacket(peer, new RequestMobilePartyBehaviorPacket(data));

        Assert.Empty(messageBroker.GetMessagesFromType<UpdatePartyBehavior>());
    }

    [Theory]
    [InlineData(InvalidSnapshotKind.NewBehaviorEnum)]
    [InlineData(InvalidSnapshotKind.NewBehaviorSentinel)]
    [InlineData(InvalidSnapshotKind.DefaultBehaviorEnum)]
    [InlineData(InvalidSnapshotKind.DefaultBehaviorSentinel)]
    [InlineData(InvalidSnapshotKind.NavigationEnum)]
    [InlineData(InvalidSnapshotKind.MoveModeEnum)]
    [InlineData(InvalidSnapshotKind.InteractableKindEnum)]
    [InlineData(InvalidSnapshotKind.TargetFlagWithoutReference)]
    [InlineData(InvalidSnapshotKind.ReferenceWithoutTargetFlag)]
    [InlineData(InvalidSnapshotKind.PartyModeWithoutMoveTarget)]
    [InlineData(InvalidSnapshotKind.MoveTargetOutsidePartyMode)]
    [InlineData(InvalidSnapshotKind.PartyBehaviorWithoutTargetParty)]
    [InlineData(InvalidSnapshotKind.EngageWithoutInteractable)]
    [InlineData(InvalidSnapshotKind.EngageWithSettlementInteractable)]
    [InlineData(InvalidSnapshotKind.EngageWithDifferentMoveTarget)]
    [InlineData(InvalidSnapshotKind.PortWithoutTargetSettlement)]
    [InlineData(InvalidSnapshotKind.UnresolvedReference)]
    [InlineData(InvalidSnapshotKind.NonFinitePosition)]
    public void MalformedSnapshot_IsRejected(InvalidSnapshotKind kind)
    {
        var data = CreateInvalidSnapshot(kind);

        handler.HandlePacket(peer, new RequestMobilePartyBehaviorPacket(data));

        Assert.Empty(messageBroker.GetMessagesFromType<UpdatePartyBehavior>());
    }

    private MobileParty RegisterParty(string id)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        Assert.True(objectManager.AddExisting(id, party));
        return party;
    }

    private void RegisterMobilePartyBase(string id, MobileParty mobileParty)
    {
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        partyBase.MobileParty = mobileParty;
        mobileParty.Party = partyBase;
        Assert.True(objectManager.AddExisting(id, partyBase));
    }

    private void RegisterSettlementPartyBase(string id)
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        partyBase.Settlement = settlement;
        settlement.Party = partyBase;
        Assert.True(objectManager.AddExisting(id, partyBase));
    }

    private static PartyBehaviorUpdateData CreateSnapshot(
        string mobilePartyId = CompactPartyId,
        AiBehavior newBehavior = AiBehavior.Hold,
        AiBehavior defaultBehavior = AiBehavior.Hold,
        MobileParty.NavigationType navigationType = MobileParty.NavigationType.None,
        bool hasTarget = false,
        string interactablePointId = null)
    {
        var point = new CampaignVec2(new Vec2(10f, 20f), true);
        return new PartyBehaviorUpdateData(
            mobilePartyId,
            newBehavior,
            interactablePointId,
            point,
            hasTarget,
            point,
            defaultBehavior,
            point,
            navigationType)
        {
            MoveTargetPoint = point,
            NextTargetPosition = point,
            PartyMoveMode = MoveModeType.Hold,
        };
    }

    private static PartyBehaviorUpdateData CreateInvalidSnapshot(InvalidSnapshotKind kind)
    {
        var data = kind switch
        {
            InvalidSnapshotKind.NewBehaviorEnum =>
                CreateSnapshot(newBehavior: (AiBehavior)int.MaxValue),
            InvalidSnapshotKind.NewBehaviorSentinel =>
                CreateSnapshot(newBehavior: AiBehavior.NumAiBehaviors),
            InvalidSnapshotKind.DefaultBehaviorEnum =>
                CreateSnapshot(defaultBehavior: (AiBehavior)int.MaxValue),
            InvalidSnapshotKind.DefaultBehaviorSentinel =>
                CreateSnapshot(defaultBehavior: AiBehavior.NumAiBehaviors),
            InvalidSnapshotKind.NavigationEnum =>
                CreateSnapshot(navigationType: (MobileParty.NavigationType)int.MaxValue),
            InvalidSnapshotKind.TargetFlagWithoutReference =>
                CreateSnapshot(hasTarget: true),
            InvalidSnapshotKind.ReferenceWithoutTargetFlag =>
                CreateSnapshot(interactablePointId: "TargetPartyBase"),
            InvalidSnapshotKind.PartyBehaviorWithoutTargetParty =>
                CreateSnapshot(defaultBehavior: AiBehavior.EngageParty),
            InvalidSnapshotKind.EngageWithoutInteractable =>
                CreateSnapshot(newBehavior: AiBehavior.EngageParty),
            InvalidSnapshotKind.EngageWithSettlementInteractable =>
                CreateSnapshot(
                    newBehavior: AiBehavior.EngageParty,
                    hasTarget: true,
                    interactablePointId: "TargetSettlementPartyBase"),
            InvalidSnapshotKind.EngageWithDifferentMoveTarget =>
                CreateSnapshot(
                    newBehavior: AiBehavior.EngageParty,
                    hasTarget: true,
                    interactablePointId: "TargetPartyBase"),
            _ => CreateSnapshot(),
        };

        switch (kind)
        {
            case InvalidSnapshotKind.MoveModeEnum:
                data.PartyMoveMode = (MoveModeType)int.MaxValue;
                break;
            case InvalidSnapshotKind.InteractableKindEnum:
                data.InteractableKind = (BehaviorInteractableKind)int.MaxValue;
                break;
            case InvalidSnapshotKind.PartyModeWithoutMoveTarget:
                data.PartyMoveMode = MoveModeType.Party;
                break;
            case InvalidSnapshotKind.MoveTargetOutsidePartyMode:
                data.MoveTargetPartyId = "TargetParty";
                break;
            case InvalidSnapshotKind.PortWithoutTargetSettlement:
                data.IsTargetingPort = true;
                break;
            case InvalidSnapshotKind.UnresolvedReference:
                data.TargetPartyId = "MissingParty";
                break;
            case InvalidSnapshotKind.NonFinitePosition:
                data.PartyPosition = new CampaignVec2(new Vec2(float.NaN, 20f), true);
                break;
            case InvalidSnapshotKind.EngageWithSettlementInteractable:
                data.PartyMoveMode = MoveModeType.Party;
                data.MoveTargetPartyId = "TargetParty";
                break;
            case InvalidSnapshotKind.EngageWithDifferentMoveTarget:
                data.PartyMoveMode = MoveModeType.Party;
                data.MoveTargetPartyId = "OtherParty";
                break;
        }

        return data;
    }

    /// <summary>
    /// Enumerates malformed client snapshot variants exercised by validation tests.
    /// </summary>
    public enum InvalidSnapshotKind
    {
        NewBehaviorEnum,
        NewBehaviorSentinel,
        DefaultBehaviorEnum,
        DefaultBehaviorSentinel,
        NavigationEnum,
        MoveModeEnum,
        InteractableKindEnum,
        TargetFlagWithoutReference,
        ReferenceWithoutTargetFlag,
        PartyModeWithoutMoveTarget,
        MoveTargetOutsidePartyMode,
        PartyBehaviorWithoutTargetParty,
        EngageWithoutInteractable,
        EngageWithSettlementInteractable,
        EngageWithDifferentMoveTarget,
        PortWithoutTargetSettlement,
        UnresolvedReference,
        NonFinitePosition,
    }
}
