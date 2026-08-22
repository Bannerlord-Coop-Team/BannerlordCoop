using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Patches;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Issues.Patches;

public class ControllerIdPersistenceMigrationTests
{
    private const string LegacyControllerId = "76561198000000042";

    [Fact]
    public void IssueOwnershipSyncData_Load_PreservesUnscopedControllerId()
    {
        var issueGiver = ObjectHelper.SkipConstructor<Hero>();
        var ownershipRegistry = new IssueOwnershipRegistry();
        var generationRegistry = new IssueGenerationRegistry();
        var records = new Dictionary<string, object>();
        ownershipRegistry.SetOwner(issueGiver, LegacyControllerId);

        IssueOwnershipPersistencePatches.SyncDataInternal(
            new TestDataStore(isSaving: true, records),
            ownershipRegistry,
            generationRegistry);
        ownershipRegistry.ClearAll();
        IssueOwnershipPersistencePatches.SyncDataInternal(
            new TestDataStore(isSaving: false, records),
            ownershipRegistry,
            generationRegistry);

        Assert.True(ownershipRegistry.TryGetOwnerControllerId(issueGiver, out var controllerId));
        Assert.Equal(LegacyControllerId, controllerId);
    }

    [Fact]
    public void AlternativeSolutionTroopsSyncData_Load_PreservesUnscopedControllerId()
    {
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        var troops = new TroopRoster();
        troops.AddToCounts(character, 1, false, 0, 0, true);
        var troopsRegistry = new AwaitingAlternativeSolutionTroopsRegistry();
        var records = new Dictionary<string, object>();
        troopsRegistry.Restore(LegacyControllerId, troops);

        AwaitingAlternativeSolutionTroopsPersistencePatches.SyncDataInternal(
            new TestDataStore(isSaving: true, records),
            troopsRegistry);
        troopsRegistry.ClearAll();
        AwaitingAlternativeSolutionTroopsPersistencePatches.SyncDataInternal(
            new TestDataStore(isSaving: false, records),
            troopsRegistry);

        Assert.True(troopsRegistry.TryGet(LegacyControllerId, out var restored));
        Assert.Same(troops, restored);
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;

        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        internal TestDataStore(bool isSaving, Dictionary<string, object> records)
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
