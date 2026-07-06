using Common.Tests.Utils;
using Common.Util;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

public class PlayerPartyInteractionHandlerTests
{
    private readonly TestMessageBroker messageBroker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly PlayerPartyInteractionHandler handler;

    public PlayerPartyInteractionHandlerTests()
    {
        handler = new PlayerPartyInteractionHandler(messageBroker, objectManager.Object);
    }

    [Fact]
    public void ShouldInitiateReciprocalPlayerInteraction_EngagingIdSortsBeforeTarget_ReturnsTrue()
    {
        var engagingParty = CreatePartyBase();
        var targetParty = CreatePartyBase();
        SetupId(engagingParty, "party-a");
        SetupId(targetParty, "party-b");

        var shouldInitiate = handler.ShouldInitiateReciprocalPlayerInteraction(engagingParty, targetParty);

        Assert.True(shouldInitiate);
    }

    [Fact]
    public void ShouldInitiateReciprocalPlayerInteraction_EngagingIdSortsAfterTarget_ReturnsFalse()
    {
        var engagingParty = CreatePartyBase();
        var targetParty = CreatePartyBase();
        SetupId(engagingParty, "party-b");
        SetupId(targetParty, "party-a");

        var shouldInitiate = handler.ShouldInitiateReciprocalPlayerInteraction(engagingParty, targetParty);

        Assert.False(shouldInitiate);
    }

    [Fact]
    public void ShouldInitiateReciprocalPlayerInteraction_UnresolvableParty_ReturnsFalse()
    {
        var engagingParty = CreatePartyBase();
        var targetParty = CreatePartyBase();
        SetupNoId(engagingParty);

        var shouldInitiate = handler.ShouldInitiateReciprocalPlayerInteraction(engagingParty, targetParty);

        Assert.False(shouldInitiate);
    }

    [Fact]
    public void ReciprocalPlayerPartyInteractionAttempted_PublishedMessage_MarksHandled()
    {
        var engagingParty = CreatePartyBase();
        var targetParty = CreatePartyBase();
        SetupNoId(engagingParty);
        var message = new ReciprocalPlayerPartyInteractionAttempted(targetParty, engagingParty);

        messageBroker.Publish(this, message);

        Assert.True(message.Handled);
    }

    private static PartyBase CreatePartyBase()
        => ObjectHelper.SkipConstructor<PartyBase>();

    private void SetupId(object party, string id)
    {
        objectManager.Setup(o => o.TryGetId(party, out id)).Returns(true);
    }

    private void SetupNoId(object party)
    {
        string unused = string.Empty;
        objectManager.Setup(o => o.TryGetId(party, out unused)).Returns(false);
    }
}