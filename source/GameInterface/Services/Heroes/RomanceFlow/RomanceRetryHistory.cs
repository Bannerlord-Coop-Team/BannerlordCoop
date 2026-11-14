using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.RomanceFlow;

/// <summary>
/// The server's failed courtships, one per player and courted hero, saved with the campaign (#4588).
/// Vanilla keys its failures by the courted hero only, so one player's failure would hold back every
/// other player's retry with that hero.
/// </summary>
internal interface IRomanceRetryHistory
{
    void RecordFailure(Hero playerHero, Hero courtedHero, CampaignTime time);
    void Clear(Hero playerHero, Hero courtedHero);
    bool TryGetFailure(Hero playerHero, Hero courtedHero, out CampaignTime time);

    /// <summary>Writes the failures on a save; on a load, replaces them when <paramref name="restore"/> is true.</summary>
    void Sync(IDataStore dataStore, bool restore);
}

internal class RomanceRetryHistory : IRomanceRetryHistory
{
    internal const string FailuresKey = "coop_romance_failed_courtships";

    private readonly Dictionary<string, long> failureTicks = new();

    private static string Key(Hero playerHero, Hero courtedHero) => playerHero.StringId + "|" + courtedHero.StringId;

    public void RecordFailure(Hero playerHero, Hero courtedHero, CampaignTime time) =>
        failureTicks[Key(playerHero, courtedHero)] = time.NumTicks;

    public void Clear(Hero playerHero, Hero courtedHero) => failureTicks.Remove(Key(playerHero, courtedHero));

    public bool TryGetFailure(Hero playerHero, Hero courtedHero, out CampaignTime time)
    {
        var found = failureTicks.TryGetValue(Key(playerHero, courtedHero), out var ticks);
        time = found ? new CampaignTime(ticks) : CampaignTime.Never;
        return found;
    }

    public void Sync(IDataStore dataStore, bool restore)
    {
        Dictionary<string, string> saved = null;
        if (dataStore.IsSaving)
        {
            saved = new Dictionary<string, string>();
            foreach (var entry in failureTicks)
                saved[entry.Key] = entry.Value.ToString(CultureInfo.InvariantCulture);
        }

        dataStore.SyncData(FailuresKey, ref saved);
        if (!dataStore.IsLoading || !restore) return;

        failureTicks.Clear();
        if (saved == null) return;
        foreach (var entry in saved)
        {
            if (long.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) && ticks > 0)
                failureTicks[entry.Key] = ticks;
        }
    }
}

/// <summary>Vanilla's <see cref="RomanceCampaignBehavior"/> data store carries the server's failures.</summary>
[HarmonyPatch(typeof(RomanceCampaignBehavior), nameof(RomanceCampaignBehavior.SyncData))]
internal class RomanceRetryHistorySavePatch
{
    // Only the server restores: a client gets its own failures with the romance states.
    [HarmonyPostfix]
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IRomanceRetryHistory>(out var history))
            history.Sync(dataStore, restore: ModInformation.IsServer);
    }
}
