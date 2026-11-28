using GameInterface.Services.Issues.Framework.Registries;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

/// <summary>#3982: the issue owners survive a save and a server restart, so the dismisser keeps an owned alternative solution.</summary>
public class IssueOwnershipPersistenceTests
{
    [Fact]
    public void ServerLoad_RestoresEveryOwnerTheSaveWrote()
    {
        var saved = new IssueOwnershipRegistry();
        saved.TrySetOwner("notable_1", "issue_1", "player-A");
        saved.TrySetOwner("notable_2", "issue_2", "player-B");
        var records = new Dictionary<string, object>();
        IssueOwnershipPersistence.Sync(new TestDataStore(isSaving: true, records), saved, restore: true);

        var loaded = new IssueOwnershipRegistry();
        IssueOwnershipPersistence.Sync(new TestDataStore(isSaving: false, records), loaded, restore: true);

        Assert.True(loaded.TryGetOwner("notable_1", "issue_1", out var first));
        Assert.Equal("player-A", first);
        Assert.True(loaded.TryGetOwner("notable_2", "issue_2", out var second));
        Assert.Equal("player-B", second);
        Assert.True(loaded.TryGetIssueOwnerId("issue_2", out var issueOwnerId));
        Assert.Equal("notable_2", issueOwnerId);
    }

    [Fact]
    public void ClientLoad_LeavesTheOwnersToTheJoinSnapshot()
    {
        var saved = new IssueOwnershipRegistry();
        saved.TrySetOwner("notable_1", "issue_1", "player-A");
        var records = new Dictionary<string, object>();
        IssueOwnershipPersistence.Sync(new TestDataStore(isSaving: true, records), saved, restore: true);

        var loaded = new IssueOwnershipRegistry();
        IssueOwnershipPersistence.Sync(new TestDataStore(isSaving: false, records), loaded, restore: false);

        Assert.Empty(loaded.GetAll());
    }

    [Fact]
    public void LoadOfASaveWithoutOwners_RestoresNothing()
    {
        var loaded = new IssueOwnershipRegistry();

        IssueOwnershipPersistence.Sync(new TestDataStore(isSaving: false, new Dictionary<string, object>()), loaded, restore: true);

        Assert.Empty(loaded.GetAll());
    }

    [Fact]
    public void SavePatch_TargetsTheVanillaIssuesBehavior()
    {
        var harmony = new Harmony("coop.tests.issues.ownership-save");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(IssueOwnershipSavePatch)).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;

        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        public TestDataStore(bool isSaving, Dictionary<string, object> records)
        {
            IsSaving = isSaving;
            this.records = records;
        }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving)
            {
                records[key] = data;
                return true;
            }

            if (!records.TryGetValue(key, out var value)) return false;
            data = (T)value;
            return true;
        }
    }
}
