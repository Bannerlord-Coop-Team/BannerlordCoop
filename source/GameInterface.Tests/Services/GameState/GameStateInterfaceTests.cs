using Common.Messaging;
using Common.Util;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Modules;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

// Shares DebugPartyVisibility with the other visibility tests.
[Collection(ModInformationRoleCollection.Name)]
public class GameStateInterfaceTests
{
    static GameStateInterfaceTests()
    {
        // EndGame marshals to the game thread; start the Coop.Tests pump even when this class runs alone.
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void EndGame_DoesNotPublishMainMenuEnteredBeforeInitialStateActivation()
    {
        var messageBroker = new Mock<IMessageBroker>();
        bool endGameCalled = false;
        var gameStateInterface = new GameStateInterface(
            messageBroker.Object,
            () => endGameCalled = true,
            System.Array.Empty<SaveGameFileInfo>,
            _ => { },
            Mock.Of<INavalSaveCompatibility>(),
            Mock.Of<ILoadingInterface>());

        gameStateInterface.EndGame();

        Assert.True(endGameCalled);
        messageBroker.Verify(
            broker => broker.Publish(
                It.IsAny<object>(),
                It.IsAny<MainMenuEntered>()),
            Times.Never);
    }

    // The forced load skips vanilla's NavalDLC check, so a mismatched save must stop before it and leave the loading screen.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadGame_NavalMismatch_RefusesBeforeTheForcedLoad(bool orStartNew)
    {
        var saveCheck = new Mock<INavalSaveCompatibility>();
        saveCheck.Setup(check => check.GetHostRefusal("vanilla", new[] { "Native", "Sandbox" })).Returns("refused");
        var loading = new Mock<ILoadingInterface>();
        var forcedLoads = new List<SaveGameFileInfo>();
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => { },
            () => new[] { Save("vanilla", "Native;Sandbox") }, forcedLoads.Add, saveCheck.Object, loading.Object);

        Load(gameStateInterface, "vanilla", orStartNew);

        Assert.Empty(forcedLoads);
        loading.Verify(value => value.HideLoadingScreen(), Times.Once);
    }

    // A save the check allows goes on to the forced load as before.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadGame_MatchingSave_ForcesTheLoad(bool orStartNew)
    {
        var loading = new Mock<ILoadingInterface>();
        var forcedLoads = new List<SaveGameFileInfo>();
        var save = Save("naval", "Native;Sandbox;NavalDLC");
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => { },
            () => new[] { save }, forcedLoads.Add, Mock.Of<INavalSaveCompatibility>(), loading.Object);

        Load(gameStateInterface, "naval", orStartNew);

        Assert.Same(save, Assert.Single(forcedLoads));
        loading.Verify(value => value.HideLoadingScreen(), Times.Never);
    }

    // Both host entry points share the save check.
    private static void Load(GameStateInterface gameStateInterface, string saveName, bool orStartNew)
    {
        if (orStartNew) gameStateInterface.LoadGameOrStartNew(saveName);
        else gameStateInterface.LoadGame(saveName);
    }

    // A save listing entry with the given module list.
    private static SaveGameFileInfo Save(string name, string modules)
    {
        var metaData = new MetaData();
        metaData.Add("Modules", modules);
        return new SaveGameFileInfo { Name = name, MetaData = metaData };
    }

    [Fact]
    public void ClearTransferredMapNotices_DropsHistoryWithoutSuppressingFutureNotices()
    {
        var informationManager = new CampaignInformationManager();
        informationManager._mapNotices.Add(new TestInformationData("Historical"));

        GameStateInterface.ClearTransferredMapNotices(informationManager);
        informationManager.OnGameLoaded();

        Assert.Empty(informationManager._mapNotices);

        var liveNotice = new TestInformationData("Live");
        informationManager.NewMapNoticeAdded(liveNotice);

        Assert.Same(liveNotice, Assert.Single(informationManager._mapNotices));
    }

    // The server's save has every party visible; release clients start them hidden, debug builds keep them all visible.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearTransferredPartyVisibility_HidesEveryPartyUnlessDebugForcesAllVisible(bool forceAllVisible)
    {
        bool wasForceAllVisible = DebugPartyVisibility.ForceAllVisible;
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.CampaignObjectManager = new CampaignObjectManager();
        for (int i = 0; i < 3; i++)
        {
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            party._isVisible = true;
            campaign.CampaignObjectManager._mobileParties.Add(party);
        }

        try
        {
            DebugPartyVisibility.ForceAllVisible = forceAllVisible;
            GameStateInterface.ClearTransferredPartyVisibility(campaign);
        }
        finally
        {
            DebugPartyVisibility.ForceAllVisible = wasForceAllVisible;
        }

        Assert.Equal(3, campaign.MobileParties.Count);
        Assert.All(campaign.MobileParties, party => Assert.Equal(forceAllVisible, party._isVisible));
    }

    // A late initialize load re-reads each party's saved fields in InitializeObjects (MobileParty.OnLoad), so the
    // clear only holds after it. Production runs it from TransferredSaveGameManager.OnGameLoaded, which this test
    // does not drive.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearTransferredPartyVisibility_HoldsOnlyAfterLateInitialize(bool clearAfterInitialize)
    {
        bool wasForceAllVisible = DebugPartyVisibility.ForceAllVisible;
        var saved = ObjectHelper.SkipConstructor<MobileParty>();
        saved.Party = ObjectHelper.SkipConstructor<PartyBase>();
        saved._isVisible = true;
        var driver = new InMemDriver();
        var saveResult = SaveManager.Save(saved, new MetaData { ["ApplicationVersion"] = "v1.4.8.0" }, "party", driver);
        Assert.True(saveResult.Successful);
        var loadResult = SaveManager.Load("party", driver, loadAsLateInitialize: true);
        var party = Assert.IsType<MobileParty>(loadResult.Root);
        Assert.True(party._isVisible);
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.CampaignObjectManager = new CampaignObjectManager();
        campaign.CampaignObjectManager._mobileParties.Add(party);

        try
        {
            DebugPartyVisibility.ForceAllVisible = false;
            if (!clearAfterInitialize) GameStateInterface.ClearTransferredPartyVisibility(campaign);
            loadResult.InitializeObjects();
            if (clearAfterInitialize) GameStateInterface.ClearTransferredPartyVisibility(campaign);
        }
        finally
        {
            DebugPartyVisibility.ForceAllVisible = wasForceAllVisible;
        }

        Assert.Equal(!clearAfterInitialize, party._isVisible);
    }

    private sealed class TestInformationData : InformationData
    {
        public TestInformationData(string description) : base(new TextObject(description))
        {
        }

        public override TextObject TitleText => DescriptionText;

        public override string SoundEventPath => string.Empty;
    }
}
