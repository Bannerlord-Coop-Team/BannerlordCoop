#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Commands;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Party;

[Collection(ModInformationRoleCollection.Name)]
public class PartyMovementFixtureCommandTests
{
    [Fact]
    public void Restore_PreservesCapturedPointAndRefusesMovedPartyOrDuplicateCapture()
    {
        bool wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            party.Party = ObjectHelper.SkipConstructor<PartyBase>();
            party.Party.MobileParty = party;
            party._position = new CampaignVec2(new Vec2(10, 20), true);
            var data = new PartyBehaviorUpdateData { PartyPosition = party.Position, PartyMoveMode = MoveModeType.Point,
                MoveTargetPoint = new CampaignVec2(new Vec2(30, 40), true) };
            var objects = new Mock<IObjectManager>();
            objects.Setup(x => x.TryGetObject("player", out party)).Returns(true);
            var snapshots = new Mock<IMobilePartyBehaviorSnapshot>();
            snapshots.Setup(x => x.TryCreate(party, out data)).Returns(true);
            IInteractablePoint interactable = null;
            snapshots.Setup(x => x.TryApply(party, data, out interactable)).Returns(true);
            var broker = new Mock<IMessageBroker>();
            var command = new PartyMovementFixtureCommand(objects.Object, snapshots.Object, broker.Object);
            var args = new CoopCommandArgsFactory();
            Assert.True(command.ProcessCommand(args.FromValues(new[] { "capture", "player" })).Succeeded);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { "capture", "player" })).Succeeded);
            party._position = new CampaignVec2(new Vec2(11, 20), true);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { "restore", "player" })).Succeeded);
            snapshots.Verify(x => x.TryApply(party, data, out interactable), Times.Never);
            party._position = data.PartyPosition;
            Assert.True(command.ProcessCommand(args.FromValues(new[] { "restore", "player" })).Succeeded);
            snapshots.Verify(x => x.TryApply(party, data, out interactable), Times.Once);
            broker.Verify(x => x.Publish(command, It.Is<PartyBehaviorChangeAttempted>(m =>
                m.Party == party && m.ForcePosition && !m.ResetMovementToHold)), Times.Once);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { "restore", "player" })).Succeeded);
        }
        finally { ModInformation.IsServer = wasServer; }
    }
}
#endif
