using Common.Util;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Factory registry backing <see cref="Patches.SimpleIssueCreationPatch"/>/<see cref="Handlers.SimpleIssueCreationHandler"/>
/// for issue types whose Issue class has a plain <c>(Hero issueOwner)</c> constructor that rolls NO field a
/// client would need captured/forced to replicate byte-identically (verified per type against the decompiled
/// source - see each type's own survey notes in the branch report). For these, "replicate this issue" needs no
/// payload beyond "which owner, which type" - a client can just call the type's own real constructor.
///
/// This still needs its OWN server-authoritative-creation-broadcast-then-replicate flow (not "just let every
/// client construct one independently") for one reason unrelated to randomness: <c>IssueManagerCreateNewIssuePatches.Prefix</c>
/// already unconditionally blocks EVERY <see cref="IssueManager.CreateNewIssue"/> call on a client (regardless
/// of issue type), so without this, a client would simply never receive one of these issues at all.
///
/// Types NOT in this registry (<c>VillageNeedsToolsIssue</c>, <c>VillageNeedsCraftingMaterialsIssue</c>,
/// <c>LordNeedsHorsesIssue</c>, <c>CapturedByBountyHuntersIssue</c>, <c>ScoutEnemyGarrissonsIssue</c>) roll or
/// reference at least one field at creation time that genuinely needs capturing+forcing, so they keep their
/// own bespoke Interface/Messages/Patches/Handler file set instead.
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
        [typeof(LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue)] = new Entry(
            "LandlordTrainingForRetainers",
            typeof(LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue),
            owner => new LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue(owner),
            IssueBase.IssueFrequency.VeryCommon),
        [typeof(GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue)] = new Entry(
            "GangLeaderNeedsRecruits",
            typeof(GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue),
            owner => new GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue(owner),
            IssueBase.IssueFrequency.VeryCommon),
        [typeof(LadysKnightOutIssueBehavior.LadysKnightOutIssue)] = new Entry(
            "LadysKnightOut",
            typeof(LadysKnightOutIssueBehavior.LadysKnightOutIssue),
            owner => new LadysKnightOutIssueBehavior.LadysKnightOutIssue(owner),
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
