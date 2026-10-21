using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.RomanceFlow;
using GameInterface.Services.Players;
using Moq;
using System;
using System.IO;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Heroes.RomanceFlow;

/// <summary>Checks confirmation, cancellation and stale marriage handling before a divorce request is sent.</summary>
[Collection(ModInformationRoleCollection.Name)]
public class PlayerDivorceConfirmationTests : IDisposable
{
    private readonly GameTextManager previousTexts = GameTexts._gameTextManager;
    private readonly Mock<IPlayerDivorceRules> rules = new();
    private readonly Mock<IMessageBroker> messages = new();
    private readonly Mock<IPlayerManager> players = new();
    private readonly Hero player = ObjectHelper.SkipConstructor<Hero>();
    private readonly Hero spouse = ObjectHelper.SkipConstructor<Hero>();

    public PlayerDivorceConfirmationTests()
    {
        GameTexts._gameTextManager = new GameTextManager();
        var strings = new XmlDocument();
        strings.Load(Path.Combine(AppContext.BaseDirectory, "global_strings.xml"));
        GameTexts._gameTextManager.LoadFromXML(strings);
        player._name = new TextObject("Hermes");
        spouse._name = new TextObject("Shah");
        string reason = null!;
        rules.Setup(value => value.CanDivorce(player, spouse, out reason)).Returns(true);
    }

    public void Dispose() => GameTexts._gameTextManager = previousTexts;

    [Fact]
    public void Confirm_SendsTheMarriageShownInThePrompt()
    {
        var inquiry = Confirmation().CreateInquiry(player, spouse);
        messages.VerifyNoOtherCalls();

        Assert.Contains("Shah", inquiry.TitleText);
        inquiry.AffirmativeAction();

        messages.Verify(broker => broker.Publish(It.IsAny<object>(), It.Is<PlayerDivorceRequested>(
            request => request.PlayerHero == player && request.Spouse == spouse)), Times.Once);
        messages.VerifyNoOtherCalls();
    }

    [Fact]
    public void Cancel_DoesNotSendARequest()
    {
        var inquiry = Confirmation().CreateInquiry(player, spouse);
        inquiry.NegativeAction?.Invoke();

        messages.VerifyNoOtherCalls();
    }

    [Fact]
    public void MarriageChangesBeforeConfirmation_DoesNotSendARequest()
    {
        var inquiry = Confirmation().CreateInquiry(player, spouse);
        string reason = null!;
        rules.Setup(value => value.CanDivorce(player, spouse, out reason)).Returns(false);

        inquiry.AffirmativeAction();

        messages.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Prompt_ExplainsWhetherClanMembershipChanges(bool playerSpouse)
    {
        player._clan = ObjectHelper.SkipConstructor<Clan>();
        spouse._clan = player.Clan;
        players.Setup(manager => manager.Contains(spouse)).Returns(playerSpouse);

        var inquiry = Confirmation().CreateInquiry(player, spouse);

        Assert.Contains("Shah", inquiry.Text);
        if (playerSpouse)
        {
            Assert.Contains("original clan", inquiry.Text);
            Assert.Contains("personal gold", inquiry.Text);
            Assert.Contains("Shared settlements", inquiry.Text);
        }
        else
            Assert.Contains("keep their current clan membership", inquiry.Text);
    }

    private PlayerDivorceConfirmation Confirmation() => new(rules.Object, messages.Object, players.Object);
}
