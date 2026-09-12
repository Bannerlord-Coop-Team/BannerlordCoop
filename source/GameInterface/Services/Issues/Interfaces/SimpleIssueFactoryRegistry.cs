using Common.Util;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

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
        [typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue)] = new Entry(
            "LandLordTheArtOfTheTrade",
            typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue),
            owner => new LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue(
                owner, owner.CurrentSettlement.Village.VillageType.PrimaryProduction),
            IssueBase.IssueFrequency.VeryCommon),
        [typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue)] = new Entry(
            "RuralNotableInnAndOut",
            typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue),
            owner => new SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue(owner),
            IssueBase.IssueFrequency.Common),
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
