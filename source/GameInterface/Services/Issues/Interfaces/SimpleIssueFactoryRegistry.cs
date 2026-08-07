using Common.Util;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Factory registry backing <see cref="Patches.SimpleIssueCreationPatch"/>/<see cref="Handlers.SimpleIssueCreationHandler"/>
/// for issue types whose Issue class can be reconstructed from just the owner Hero - rolling no field a client
/// would need captured/forced to replicate byte-identically. Still needs its own server-authoritative-creation-
/// broadcast-then-replicate flow: <c>IssueManagerCreateNewIssuePatches.Prefix</c> unconditionally blocks every
/// <see cref="IssueManager.CreateNewIssue"/> call on a client, so without this a client would never receive one
/// of these issues at all.
///
/// A type NOT in this registry rolls or references at least one field (at creation, or for The Spy Party,
/// accept time) that needs capturing+forcing rather than being safely re-derivable from the owner alone, so it
/// keeps its own bespoke Interface/Messages/Patches/Handler file set instead.
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
        // Tier 1 Group 1B: LandLordNeedsManualLaborersIssue/BettingFraudIssue/GangLeaderNeedsSpecialWeaponsIssue
        // all have a plain (Hero) Issue ctor that rolls nothing - each of their real per-client-divergent rolls
        // happens later, at ACCEPT time inside GenerateIssueQuest (see each type's own bespoke
        // Interfaces/*IssueInterface.cs for the accept-time capture that actually matters).
        [typeof(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue)] = new Entry(
            "LandLordNeedsManualLaborers",
            typeof(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue),
            owner => new LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue(owner),
            IssueBase.IssueFrequency.VeryCommon),
        [typeof(BettingFraudIssueBehavior.BettingFraudIssue)] = new Entry(
            "BettingFraud",
            typeof(BettingFraudIssueBehavior.BettingFraudIssue),
            owner => new BettingFraudIssueBehavior.BettingFraudIssue(owner),
            IssueBase.IssueFrequency.Rare),
        [typeof(GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue)] = new Entry(
            "GangLeaderNeedsSpecialWeapons",
            typeof(GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue),
            owner => new GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue(owner),
            IssueBase.IssueFrequency.VeryCommon),
        // Tier 1 Group 1C: LandLordTheArtOfTheTradeIssue's ctor takes an extra ItemObject param, but it's never
        // rolled - it's a pure, deterministic derivation of the owner's own CurrentSettlement
        // (Village.VillageType.PrimaryProduction), the SAME derivation vanilla's own OnGameLoad() independently
        // re-runs on every load (this field isn't even a [SaveableField] - confirmed by the decompiled source -
        // vanilla itself treats it as safely recomputable from the owner alone, never needing persistence). A
        // client reconstructing via this lambda at the moment it receives the creation broadcast lands on the
        // exact same value the server did, without needing a bespoke Interfaces/Messages/Patches/Handler file
        // set the way NearbyBanditBaseIssue's target-hideout pick (NOT re-derivable - see its own bespoke
        // interface) needed.
        [typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue)] = new Entry(
            "LandLordTheArtOfTheTrade",
            typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue),
            owner => new LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue(
                owner, owner.CurrentSettlement.Village.VillageType.PrimaryProduction),
            IssueBase.IssueFrequency.VeryCommon),
        // Tier 1 Group 1D: RuralNotableInnAndOutIssue (SandBox.dll) has a genuinely plain (Hero) ctor - its
        // _targetSettlement/_boardGameType fields are derived the same "safely recomputable, not a
        // [SaveableField]" way as above (confirmed by its own OnGameLoad() override, which just re-derives them
        // instead of expecting them restored).
        [typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue)] = new Entry(
            "RuralNotableInnAndOut",
            typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue),
            owner => new SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue(owner),
            IssueBase.IssueFrequency.Common),
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
