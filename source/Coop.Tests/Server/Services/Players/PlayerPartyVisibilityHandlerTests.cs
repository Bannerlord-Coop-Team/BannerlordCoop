using Common;
using Common.Network;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Players.Handlers;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.Server.Services.Players;

[Collection(ModInformationRoleCollection.Name)]
public class PlayerPartyVisibilityHandlerTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly Campaign? previousCampaign;
    private readonly Type sandBoxViewSubModuleType;
    private readonly object? previousSandBoxViewSubModule;

    public PlayerPartyVisibilityHandlerTests()
    {
        ModInformation.IsServer = true;
        previousCampaign = Campaign.Current;

        var campaign = (Campaign)FormatterServices.GetUninitializedObject(typeof(Campaign));
        campaign.CampaignEventDispatcher = new CampaignEventDispatcher(Array.Empty<CampaignEventReceiver>());
        Campaign.Current = campaign;

        sandBoxViewSubModuleType = Type.GetType(
            "SandBox.View.SandBoxViewSubModule, SandBox.View",
            throwOnError: true)!;
        var visualManagerType = Type.GetType(
            "SandBox.View.SandBoxViewVisualManager, SandBox.View",
            throwOnError: true)!;
        var instanceField = AccessTools.Field(sandBoxViewSubModuleType, "_instance");
        previousSandBoxViewSubModule = instanceField.GetValue(null);

        var subModule = FormatterServices.GetUninitializedObject(sandBoxViewSubModuleType);
        AccessTools.Field(sandBoxViewSubModuleType, "_sandBoxViewVisualManager")
            .SetValue(subModule, Activator.CreateInstance(visualManagerType));
        instanceField.SetValue(null, subModule);
    }

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
        Campaign.Current = previousCampaign;
        AccessTools.Field(sandBoxViewSubModuleType, "_instance")
            .SetValue(null, previousSandBoxViewSubModule);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SavedPlayerRegistrationsRestored_PersistsOfflinePartyAsInactiveAndHidden(bool isActive)
    {
        var player = CreatePlayer();
        var party = CreateParty(isActive);
        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { player });
        playerManager.Setup(manager => manager.IsConnected(player)).Returns(false);

        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.MobilePartyId, out party))
            .Returns(true);

        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            objectManager.Object,
            Mock.Of<INetwork>());

        broker.Publish(this, new SavedPlayerRegistrationsRestored());

        Assert.False(party.IsActive);
        Assert.False(party.IsVisible);
    }

    [Fact]
    public void SavedPlayerRegistrationsRestored_LeavesConnectedPartyActive()
    {
        var player = CreatePlayer();
        var party = CreateParty();
        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { player });
        playerManager.Setup(manager => manager.IsConnected(player)).Returns(true);

        var objectManager = new Mock<IObjectManager>();
        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            objectManager.Object,
            Mock.Of<INetwork>());

        broker.Publish(this, new SavedPlayerRegistrationsRestored());

        Assert.True(party.IsActive);
        objectManager.VerifyNoOtherCalls();
    }

    private static Player CreatePlayer() =>
        new Player("PlayerOne", "Hero_One", "Party_One", "Clan_One", "Character_One");

    private static MobileParty CreateParty(bool isActive = true)
    {
        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        party.Party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        party.Party.MobileParty = party;
        party.IsActive = isActive;
        party._isVisible = true;
        return party;
    }
}
