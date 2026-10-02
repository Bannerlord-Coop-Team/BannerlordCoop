using System.Collections.Generic;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ArmyNeedsSuppliesJournalOwnersTests
{
    [Fact]
    public void LoadingSavedOwnershipHidesOnlyAnotherPlayersCompletedJournal()
    {
        var own = ObjectHelper.SkipConstructor<JournalLogEntry>();
        var other = ObjectHelper.SkipConstructor<JournalLogEntry>();
        var unrelated = ObjectHelper.SkipConstructor<JournalLogEntry>();
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(x => x.ControllerId).Returns("player-a");
        var owners = new ArmyNeedsSuppliesJournalOwners(controller.Object);
        var saved = new List<ArmyNeedsSuppliesJournalOwnerSaveData>
        {
            new(own, "player-a"),
            new(other, "player-b"),
        };

        owners.SyncData(new LoadingStore(saved));

        Assert.True(owners.IsVisible(own));
        Assert.False(owners.IsVisible(other));
        Assert.True(owners.IsVisible(unrelated));
        controller.SetupGet(x => x.ControllerId).Returns("player-b");
        Assert.False(owners.IsVisible(own));
        Assert.True(owners.IsVisible(other));
    }

    [Fact]
    public void LoadingAnotherSaveClearsThePreviousCampaignOwnership()
    {
        var journal = ObjectHelper.SkipConstructor<JournalLogEntry>();
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(x => x.ControllerId).Returns("player-a");
        var owners = new ArmyNeedsSuppliesJournalOwners(controller.Object);
        owners.SetOwner(journal, "player-b");

        owners.SyncData(new LoadingStore(null));

        Assert.True(owners.IsVisible(journal));
    }

    private sealed class LoadingStore : IDataStore
    {
        private readonly List<ArmyNeedsSuppliesJournalOwnerSaveData> entries;
        public bool IsSaving => false;
        public bool IsLoading => true;

        public LoadingStore(List<ArmyNeedsSuppliesJournalOwnerSaveData> entries) => this.entries = entries;

        public bool SyncData<T>(string key, ref T data)
        {
            Assert.Equal("_coop_army_supply_journal_owners", key);
            data = (T)(object)entries;
            return entries != null;
        }
    }
}
