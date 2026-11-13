using Common.Util;
using GameInterface.Services.Heroes.RomanceFlow;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes.RomanceFlow;

/// <summary>#4588: each player's failed courtship survives a save and a server restart.</summary>
public class RomanceRetryHistoryTests
{
    private static Hero NewHero(string stringId)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero.StringId = stringId;
        return hero;
    }

    private readonly Hero first = NewHero("player_1");
    private readonly Hero second = NewHero("player_2");
    private readonly Hero courted = NewHero("lady_1");

    [Fact]
    public void ServerLoad_RestoresEachPlayersFailure()
    {
        var saved = new RomanceRetryHistory();
        saved.RecordFailure(first, courted, new CampaignTime(100));
        saved.RecordFailure(second, courted, new CampaignTime(200));
        var records = new Dictionary<string, object>();
        saved.Sync(new TestDataStore(isSaving: true, records), restore: true);

        var loaded = new RomanceRetryHistory();
        loaded.Sync(new TestDataStore(isSaving: false, records), restore: true);

        Assert.True(loaded.TryGetFailure(first, courted, out var firstTime));
        Assert.Equal(100, firstTime.NumTicks);
        Assert.True(loaded.TryGetFailure(second, courted, out var secondTime));
        Assert.Equal(200, secondTime.NumTicks);
    }

    [Fact]
    public void ClientLoad_RestoresNothing()
    {
        var saved = new RomanceRetryHistory();
        saved.RecordFailure(first, courted, new CampaignTime(100));
        var records = new Dictionary<string, object>();
        saved.Sync(new TestDataStore(isSaving: true, records), restore: true);

        var loaded = new RomanceRetryHistory();
        loaded.Sync(new TestDataStore(isSaving: false, records), restore: false);

        Assert.False(loaded.TryGetFailure(first, courted, out _));
    }

    [Fact]
    public void Clear_ForgetsOnlyThatPlayersFailure()
    {
        var history = new RomanceRetryHistory();
        history.RecordFailure(first, courted, new CampaignTime(100));
        history.RecordFailure(second, courted, new CampaignTime(200));

        history.Clear(first, courted);

        Assert.False(history.TryGetFailure(first, courted, out _));
        Assert.True(history.TryGetFailure(second, courted, out _));
    }

    [Fact]
    public void SavePatch_TargetsTheVanillaRomanceBehavior()
    {
        var harmony = new Harmony("coop.tests.romance.retry-save");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(RomanceRetryHistorySavePatch)).Patch());
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
