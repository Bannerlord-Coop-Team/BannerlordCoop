using Common.Util;
using GameInterface.Services.Issues.Patches;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class SmugglersJournalVisibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VisibleActiveOrArchivedSelectionRemainsUnchanged(bool archived)
    {
        var tracker = new Mock<IViewDataTracker>(MockBehavior.Strict);
        var viewModel = ObjectHelper.SkipConstructor<QuestsVM>();
        var selectedQuest = ObjectHelper.SkipConstructor<QuestItemVM>();
        viewModel.ActiveQuestsList = new MBBindingList<QuestItemVM>();
        viewModel.OldQuestsList = new MBBindingList<QuestItemVM>();
        (archived ? viewModel.OldQuestsList : viewModel.ActiveQuestsList).Add(selectedQuest);
        viewModel.SelectedQuest = selectedQuest;
        viewModel.CurrentQuestTitle = "own quest";

        SmugglersJournalVisibilityPatch.RefreshSelection(viewModel, tracker.Object);

        Assert.Same(selectedQuest, viewModel.SelectedQuest);
        Assert.Equal("own quest", viewModel.CurrentQuestTitle);
        tracker.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyJournalClearsFilteredSelectionWithoutSelectingNull(bool hadSelectedQuest)
    {
        var tracker = new Mock<IViewDataTracker>(MockBehavior.Strict);
        tracker.Setup(value => value.SetQuestSelection(null));
        var viewModel = ObjectHelper.SkipConstructor<QuestsVM>();
        var removedQuest = ObjectHelper.SkipConstructor<QuestItemVM>();
        removedQuest.IsSelected = true;
        viewModel.ActiveQuestsList = new MBBindingList<QuestItemVM>();
        viewModel.OldQuestsList = new MBBindingList<QuestItemVM>();
        viewModel.CurrentQuestStages = new MBBindingList<QuestStageVM>
        {
            ObjectHelper.SkipConstructor<QuestStageVM>()
        };
        viewModel.SelectedQuest = hadSelectedQuest ? removedQuest : null;
        viewModel.CurrentQuestGiverHero = ObjectHelper.SkipConstructor<HeroVM>();
        viewModel.CurrentQuestTitle = "another player's quest";
        viewModel.IsCurrentQuestGiverHeroHidden = false;

        SmugglersJournalVisibilityPatch.RefreshSelection(viewModel, tracker.Object);

        Assert.Null(viewModel.SelectedQuest);
        Assert.Empty(viewModel.CurrentQuestStages);
        Assert.Null(viewModel.CurrentQuestGiverHero);
        Assert.Empty(viewModel.CurrentQuestTitle);
        Assert.True(viewModel.IsCurrentQuestGiverHeroHidden);
        if (hadSelectedQuest) Assert.False(removedQuest.IsSelected);
        tracker.Verify(value => value.SetQuestSelection(null), Times.Once);
        tracker.VerifyNoOtherCalls();
    }
}
