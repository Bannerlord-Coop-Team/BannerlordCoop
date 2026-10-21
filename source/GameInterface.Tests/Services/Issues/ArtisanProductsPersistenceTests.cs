using Common.Util;
using GameInterface.Services.Issues.Quests.ArtisanProducts;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ArtisanProductsPersistenceTests
{
    [Fact]
    public void DeferredTroopSaveData_RoundTripsControllerRosterFields()
    {
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        var records = new Dictionary<string, object>();
        var expected = new List<ArtisanProductsPendingTroopSaveData>
        {
            new ArtisanProductsPendingTroopSaveData("player-A", character, 3, 1, 27),
        };

        var saving = new TestDataStore(true, records);
        saving.SyncData("pending", ref expected);

        var restored = default(List<ArtisanProductsPendingTroopSaveData>);
        new TestDataStore(false, records).SyncData("pending", ref restored);

        var entry = Assert.Single(restored);
        Assert.Equal("player-A", entry.ControllerId);
        Assert.Same(character, entry.Character);
        Assert.Equal(3, entry.Number);
        Assert.Equal(1, entry.WoundedNumber);
        Assert.Equal(27, entry.Xp);
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;
        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        public TestDataStore(bool isSaving, Dictionary<string, object> records)
        { IsSaving = isSaving; this.records = records; }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving) { records[key] = data; return true; }
            if (!records.TryGetValue(key, out var value)) return false;
            data = (T)value;
            return true;
        }
    }
}
