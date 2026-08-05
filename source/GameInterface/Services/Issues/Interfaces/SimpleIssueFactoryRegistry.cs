using Common.Util;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Factory registry backing <see cref="Patches.SimpleIssueCreationPatch"/>/<see cref="Handlers.SimpleIssueCreationHandler"/>
/// for issue types whose Issue class can be reconstructed from JUST the owner Hero - rolling NO field a client
/// would need captured/forced to replicate byte-identically (verified per type against the decompiled source -
/// see each type's own survey notes in the branch report). For these, "replicate this issue" needs no payload
/// beyond "which owner, which type" - a client can just call the type's own real constructor (most entries have
/// a plain <c>(Hero issueOwner)</c> ctor; <c>LandLordTheArtOfTheTradeIssue</c> below is the one exception - its
/// ctor takes an extra, deterministically-owner-derivable argument, still safely coverable by a
/// <c>Func&lt;Hero, IssueBase&gt;</c> lambda that derives it inline).
///
/// This still needs its OWN server-authoritative-creation-broadcast-then-replicate flow (not "just let every
/// client construct one independently") for one reason unrelated to randomness: <c>IssueManagerCreateNewIssuePatches.Prefix</c>
/// already unconditionally blocks EVERY <see cref="IssueManager.CreateNewIssue"/> call on a client (regardless
/// of issue type), so without this, a client would simply never receive one of these issues at all.
///
/// Types NOT in this registry (<c>VillageNeedsToolsIssue</c>, <c>VillageNeedsCraftingMaterialsIssue</c>,
/// <c>LordNeedsHorsesIssue</c>, <c>HeadmanVillageNeedsDraughtAnimalsIssue</c>) roll or reference at least one
/// field at creation time that genuinely needs capturing+forcing rather than being safely re-derivable from
/// the owner alone, so they keep their own bespoke Interface/Messages/Patches/Handler file set instead.
/// <c>HeadmanNeedsGrainIssue</c> (Village Needs Grain Seeds) IS registered here - its ctor rolls nothing - but
/// it needs its OWN small, standalone piece of infrastructure the others don't: the behavior-singleton
/// <c>_averageGrainPriceInCalradia</c> cache feeding its dialogue/reward math is NOT per-issue-instance state
/// at all, so it can't be captured/forced through this per-issue creation path - see
/// <see cref="Patches.HeadmanNeedsGrainPriceCachePatches"/>/<see cref="Patches.HeadmanNeedsGrainPricePersistencePatches"/>.
/// <c>GangLeaderNeedsSpecialWeaponsIssue</c> IS registered here for CREATION (its Issue ctor rolls nothing) but
/// still keeps its own bespoke ACCEPT-time capture file - see that type's own Interfaces/*IssueInterface.cs
/// doc comment.
/// </summary>
internal static class SimpleIssueFactoryRegistry
{
    private sealed class Entry
    {
        public readonly string Key;
        public readonly Type IssueType;
        public readonly Func<Hero, IssueBase> Factory;
        public readonly IssueBase.IssueFrequency Frequency;

        public Entry(string key, Type issueType, Func<Hero, IssueBase> factory, IssueBase.IssueFrequency frequency)
        {
            Key = key;
            IssueType = issueType;
            Factory = factory;
            Frequency = frequency;
        }
    }

    private static readonly Dictionary<Type, Entry> ByType = new Dictionary<Type, Entry>
    {
        [typeof(ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue)] = new Entry(
            "ArmyNeedsSupplies",
            typeof(ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue),
            owner => new ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue(owner),
            IssueBase.IssueFrequency.VeryCommon),
        // GangLeaderNeedsSpecialWeaponsIssue has a plain (Hero) Issue ctor that rolls nothing - its real
        // per-client-divergent roll happens later, at ACCEPT time inside GenerateIssueQuest (see that type's
        // own bespoke Interfaces/GangLeaderNeedsSpecialWeaponsIssueInterface.cs for the accept-time capture
        // that actually matters).
        [typeof(GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue)] = new Entry(
            "GangLeaderNeedsSpecialWeapons",
            typeof(GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue),
            owner => new GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue(owner),
            IssueBase.IssueFrequency.VeryCommon),
        // LandLordTheArtOfTheTradeIssue's ctor takes an extra ItemObject param, but it's never rolled - it's a
        // pure, deterministic derivation of the owner's own CurrentSettlement
        // (Village.VillageType.PrimaryProduction), the SAME derivation vanilla's own OnGameLoad() independently
        // re-runs on every load (this field isn't even a [SaveableField] - confirmed by the decompiled source -
        // vanilla itself treats it as safely recomputable from the owner alone, never needing persistence). A
        // client reconstructing via this lambda at the moment it receives the creation broadcast lands on the
        // exact same value the server did, without needing a bespoke Interfaces/Messages/Patches/Handler file
        // set.
        [typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue)] = new Entry(
            "LandLordTheArtOfTheTrade",
            typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue),
            owner => new LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue(
                owner, owner.CurrentSettlement.Village.VillageType.PrimaryProduction),
            IssueBase.IssueFrequency.VeryCommon),
        // Village Needs Grain Seeds: HeadmanNeedsGrainIssue's ctor is genuinely EMPTY (just the base(owner,
        // CampaignTime.DaysFromNow(30f)) call) - NeededGrainAmount/AlternativeSolutionNeededGold are pure
        // getters computed on demand from base.IssueDifficultyMultiplier (itself a deterministic function of
        // Campaign.Current.PlayerProgress, never an ambient roll - see DefaultIssueModel.GetIssueDifficultyMultiplier)
        // and the behavior-singleton _averageGrainPriceInCalradia (see HeadmanNeedsGrainPriceCachePatches for
        // how THAT stays byte-identical across peers - a separate, standalone mechanism, since it's owned by
        // the BEHAVIOR, not this Issue instance). Nothing here is rolled at construction time - the cheapest
        // creation-side integration of any type in this registry.
        [typeof(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue)] = new Entry(
            "HeadmanNeedsGrain",
            typeof(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue),
            owner => new HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue(owner),
            IssueBase.IssueFrequency.Common),
    };

    private static readonly Dictionary<string, Entry> ByKey = BuildByKey();

    private static Dictionary<string, Entry> BuildByKey()
    {
        var byKey = new Dictionary<string, Entry>();
        foreach (var entry in ByType.Values)
        {
            byKey[entry.Key] = entry;
        }
        return byKey;
    }

    internal static bool IsRegistered(IssueBase issue) => issue != null && ByType.ContainsKey(issue.GetType());

    internal static bool TryGetKey(IssueBase issue, out string key)
    {
        key = null;
        if (issue == null || !ByType.TryGetValue(issue.GetType(), out var entry)) return false;
        key = entry.Key;
        return true;
    }

    /// <summary>
    /// Builds (via the real constructor - safe, since these types roll nothing that needs forcing) and
    /// registers a replicated issue for <paramref name="key"/>/<paramref name="owner"/>, replaying
    /// <see cref="IssueManager.CreateNewIssue"/>'s own bookkeeping via a custom <see cref="PotentialIssueData"/> -
    /// same technique as e.g. <see cref="VillageNeedsToolsIssueInterface.RegisterReplicated"/>. Returns false
    /// (no-op) if <paramref name="key"/> is unknown.
    /// </summary>
    internal static bool TryConstructAndRegister(string key, Hero owner)
    {
        if (owner == null || !ByKey.TryGetValue(key, out var entry)) return false;

        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => entry.Factory(_owner);
        var pid = new PotentialIssueData(factory, entry.IssueType, entry.Frequency);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
        return true;
    }
}
