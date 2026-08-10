using Common.Logging;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms;

public interface ISettlementClaimantSnapshotRegistry
{
    bool TryRegister(
        SettlementClaimantDecision decision,
        IReadOnlyList<SettlementClaimantCandidateData> candidates);
    bool TryGetSnapshot(
        SettlementClaimantDecision decision,
        out IReadOnlyList<SettlementClaimantCandidate> candidates);
    bool TryCreateOutcomes(
        SettlementClaimantDecision decision,
        out MBList<DecisionOutcome> outcomes);
    void Capture(SettlementClaimantDecision decision, MBList<DecisionOutcome> outcomes);
    bool TryCreateJoinSnapshots(out SettlementClaimantDecisionSnapshotData[] snapshots);
    bool TryApplyJoinSnapshots(IReadOnlyList<SettlementClaimantDecisionSnapshotData> snapshots);
}

public sealed class SettlementClaimantCandidate
{
    public Clan Clan { get; }
    public float InitialMerit { get; }

    public SettlementClaimantCandidate(Clan clan, float initialMerit)
    {
        Clan = clan;
        InitialMerit = initialMerit;
    }
}

internal sealed class SettlementClaimantSnapshotRegistry : ISettlementClaimantSnapshotRegistry
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementClaimantSnapshotRegistry>();

    private readonly IObjectManager objectManager;
    private readonly ConditionalWeakTable<SettlementClaimantDecision, Snapshot> snapshots = new();
    private readonly object syncRoot = new();

    public SettlementClaimantSnapshotRegistry(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryRegister(
        SettlementClaimantDecision decision,
        IReadOnlyList<SettlementClaimantCandidateData> candidates)
    {
        if (decision == null || candidates == null)
        {
            Logger.Error("Cannot register a settlement claimant snapshot without a decision and candidates.");
            return false;
        }

        if (!TryResolveCandidates(candidates, out var resolvedCandidates)) return false;
        return TryRegisterResolved(decision, resolvedCandidates);
    }

    public bool TryGetSnapshot(
        SettlementClaimantDecision decision,
        out IReadOnlyList<SettlementClaimantCandidate> candidates)
    {
        if (decision != null && snapshots.TryGetValue(decision, out Snapshot snapshot))
        {
            candidates = snapshot.Candidates;
            return true;
        }

        candidates = null;
        return false;
    }

    public bool TryCreateOutcomes(
        SettlementClaimantDecision decision,
        out MBList<DecisionOutcome> outcomes)
    {
        outcomes = null;
        if (!TryGetSnapshot(decision, out IReadOnlyList<SettlementClaimantCandidate> candidates)) return false;

        outcomes = new MBList<DecisionOutcome>();
        foreach (SettlementClaimantCandidate candidate in candidates)
        {
            outcomes.Add(new SettlementClaimantDecision.ClanAsDecisionOutcome(candidate.Clan)
            {
                InitialMerit = candidate.InitialMerit,
            });
        }

        return true;
    }

    public void Capture(SettlementClaimantDecision decision, MBList<DecisionOutcome> outcomes)
    {
        if (decision == null || outcomes == null) return;

        var candidates = new List<SettlementClaimantCandidate>(outcomes.Count);
        foreach (DecisionOutcome outcome in outcomes)
        {
            if (outcome is not SettlementClaimantDecision.ClanAsDecisionOutcome claimantOutcome)
            {
                Logger.Error("Settlement claimant narrowing returned an unexpected outcome type {OutcomeType}.", outcome?.GetType().FullName);
                return;
            }

            candidates.Add(new SettlementClaimantCandidate(claimantOutcome.Clan, claimantOutcome.InitialMerit));
        }

        lock (syncRoot)
        {
            if (snapshots.TryGetValue(decision, out _)) return;
            snapshots.Add(decision, new Snapshot(candidates, isSynchronized: false));
        }
    }

    public bool TryCreateJoinSnapshots(out SettlementClaimantDecisionSnapshotData[] snapshots)
    {
        var kingdoms = Campaign.Current?.CampaignObjectManager?.Kingdoms;
        if (kingdoms == null)
        {
            snapshots = Array.Empty<SettlementClaimantDecisionSnapshotData>();
            Logger.Error("Cannot create settlement claimant join snapshots without a loaded campaign.");
            return false;
        }

        return TryCreateJoinSnapshots(kingdoms, out snapshots);
    }

    internal bool TryCreateJoinSnapshots(
        IEnumerable<Kingdom> kingdoms,
        out SettlementClaimantDecisionSnapshotData[] snapshots)
    {
        snapshots = Array.Empty<SettlementClaimantDecisionSnapshotData>();
        if (kingdoms == null) return false;

        var snapshotData = new List<SettlementClaimantDecisionSnapshotData>();
        foreach (Kingdom kingdom in kingdoms)
        {
            if (kingdom == null) return false;

            MBList<KingdomDecision> decisions = kingdom._unresolvedDecisions;
            if (decisions == null) continue;

            string kingdomId = null;
            for (int decisionIndex = 0; decisionIndex < decisions.Count; decisionIndex++)
            {
                if (decisions[decisionIndex] is not SettlementClaimantDecision decision) continue;
                if (kingdomId == null && !objectManager.TryGetIdWithLogging(kingdom, out kingdomId)) return false;
                if (!TryCaptureMissingSnapshot(decision)) return false;
                if (!TryGetSnapshot(decision, out var candidates)) return false;

                var candidateData = new SettlementClaimantCandidateData[candidates.Count];
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    SettlementClaimantCandidate candidate = candidates[candidateIndex];
                    if (!objectManager.TryGetIdWithLogging(candidate.Clan, out string clanId)) return false;

                    candidateData[candidateIndex] = new SettlementClaimantCandidateData(
                        clanId,
                        candidate.InitialMerit);
                }

                snapshotData.Add(new SettlementClaimantDecisionSnapshotData(
                    kingdomId,
                    decisionIndex,
                    candidateData));
            }
        }

        snapshots = snapshotData.ToArray();
        return true;
    }

    public bool TryApplyJoinSnapshots(IReadOnlyList<SettlementClaimantDecisionSnapshotData> snapshots)
    {
        var kingdoms = Campaign.Current?.CampaignObjectManager?.Kingdoms;
        if (kingdoms == null)
        {
            Logger.Error("Cannot apply settlement claimant join snapshots without a loaded campaign.");
            return false;
        }

        return TryApplyJoinSnapshots(
            kingdoms,
            snapshots ?? Array.Empty<SettlementClaimantDecisionSnapshotData>());
    }

    internal bool TryApplyJoinSnapshots(
        IEnumerable<Kingdom> kingdoms,
        IReadOnlyList<SettlementClaimantDecisionSnapshotData> snapshots)
    {
        if (kingdoms == null || snapshots == null) return false;

        var expectedDecisions = new Dictionary<string, SettlementClaimantDecision>();
        foreach (Kingdom kingdom in kingdoms)
        {
            if (kingdom == null) return false;

            MBList<KingdomDecision> decisions = kingdom._unresolvedDecisions;
            if (decisions == null) continue;

            string kingdomId = null;
            for (int decisionIndex = 0; decisionIndex < decisions.Count; decisionIndex++)
            {
                if (decisions[decisionIndex] is not SettlementClaimantDecision decision) continue;
                if (kingdomId == null && !objectManager.TryGetIdWithLogging(kingdom, out kingdomId)) return false;

                string identity = CreateDecisionIdentity(kingdomId, decisionIndex);
                if (expectedDecisions.ContainsKey(identity))
                {
                    Logger.Error("Loaded campaign contains duplicate settlement claimant decision identities.");
                    return false;
                }
                expectedDecisions.Add(identity, decision);
            }
        }

        if (expectedDecisions.Count != snapshots.Count)
        {
            Logger.Error(
                "Settlement claimant join snapshot count {SnapshotCount} does not match loaded decision count {DecisionCount}.",
                snapshots.Count,
                expectedDecisions.Count);
            return false;
        }

        var identities = new HashSet<string>();
        var registrations = new List<SnapshotRegistration>(snapshots.Count);
        foreach (SettlementClaimantDecisionSnapshotData snapshot in snapshots)
        {
            if (snapshot == null ||
                string.IsNullOrWhiteSpace(snapshot.KingdomId) ||
                snapshot.DecisionIndex < 0 ||
                snapshot.Candidates == null)
            {
                Logger.Error("Settlement claimant join snapshot contains invalid decision data.");
                return false;
            }

            string identity = CreateDecisionIdentity(snapshot.KingdomId, snapshot.DecisionIndex);
            if (!identities.Add(identity))
            {
                Logger.Error("Settlement claimant join snapshot contains a duplicate decision identity.");
                return false;
            }

            if (!expectedDecisions.TryGetValue(identity, out SettlementClaimantDecision decision))
            {
                Logger.Error(
                    "Settlement claimant join snapshot decision index {DecisionIndex} was unavailable for kingdom {KingdomId}.",
                    snapshot.DecisionIndex,
                    snapshot.KingdomId);
                return false;
            }

            if (!TryResolveCandidates(snapshot.Candidates, out var resolvedCandidates)) return false;
            registrations.Add(new SnapshotRegistration(decision, resolvedCandidates));
        }

        return TryRegisterResolved(registrations);
    }

    private bool TryCaptureMissingSnapshot(SettlementClaimantDecision decision)
    {
        if (TryGetSnapshot(decision, out _)) return true;

        MBList<DecisionOutcome> initialCandidates = decision.DetermineInitialCandidates().ToMBList();
        MBList<DecisionOutcome> outcomes = decision.NarrowDownCandidates(initialCandidates, 3);
        Capture(decision, outcomes);
        return TryGetSnapshot(decision, out _);
    }

    private static bool SnapshotsMatch(
        IReadOnlyList<SettlementClaimantCandidate> first,
        IReadOnlyList<SettlementClaimantCandidate> second)
    {
        if (first.Count != second.Count) return false;

        for (int i = 0; i < first.Count; i++)
        {
            if (first[i].Clan != second[i].Clan || first[i].InitialMerit != second[i].InitialMerit)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryResolveCandidates(
        IReadOnlyList<SettlementClaimantCandidateData> candidates,
        out IReadOnlyList<SettlementClaimantCandidate> resolvedCandidates)
    {
        var resolved = new List<SettlementClaimantCandidate>(candidates.Count);
        var candidateIds = new HashSet<string>();
        foreach (SettlementClaimantCandidateData candidate in candidates)
        {
            if (candidate == null ||
                string.IsNullOrWhiteSpace(candidate.ClanId) ||
                !candidateIds.Add(candidate.ClanId))
            {
                resolvedCandidates = null;
                Logger.Error("Settlement claimant snapshot contains an invalid or duplicate clan id.");
                return false;
            }
            if (!objectManager.TryGetObjectWithLogging(candidate.ClanId, out Clan clan))
            {
                resolvedCandidates = null;
                return false;
            }

            resolved.Add(new SettlementClaimantCandidate(clan, candidate.InitialMerit));
        }

        resolvedCandidates = resolved;
        return true;
    }

    private bool TryRegisterResolved(
        SettlementClaimantDecision decision,
        IReadOnlyList<SettlementClaimantCandidate> candidates)
    {
        lock (syncRoot)
        {
            if (snapshots.TryGetValue(decision, out Snapshot existingSnapshot))
            {
                if (SnapshotsMatch(existingSnapshot.Candidates, candidates))
                {
                    existingSnapshot.IsSynchronized = true;
                    return true;
                }

                Logger.Error("A different settlement claimant snapshot is already registered for this decision.");
                return false;
            }

            snapshots.Add(decision, new Snapshot(candidates, isSynchronized: true));
            return true;
        }
    }

    private bool TryRegisterResolved(IReadOnlyList<SnapshotRegistration> registrations)
    {
        lock (syncRoot)
        {
            foreach (SnapshotRegistration registration in registrations)
            {
                if (snapshots.TryGetValue(registration.Decision, out Snapshot existingSnapshot) &&
                    existingSnapshot.IsSynchronized &&
                    !SnapshotsMatch(existingSnapshot.Candidates, registration.Candidates))
                {
                    Logger.Error("A different settlement claimant snapshot is already registered for this decision.");
                    return false;
                }
            }

            foreach (SnapshotRegistration registration in registrations)
            {
                if (!snapshots.TryGetValue(registration.Decision, out Snapshot existingSnapshot))
                {
                    snapshots.Add(
                        registration.Decision,
                        new Snapshot(registration.Candidates, isSynchronized: true));
                    continue;
                }

                if (SnapshotsMatch(existingSnapshot.Candidates, registration.Candidates))
                {
                    existingSnapshot.IsSynchronized = true;
                    continue;
                }

                snapshots.Remove(registration.Decision);
                snapshots.Add(
                    registration.Decision,
                    new Snapshot(registration.Candidates, isSynchronized: true));
            }
        }

        return true;
    }

    private static string CreateDecisionIdentity(string kingdomId, int decisionIndex) =>
        kingdomId + "\n" + decisionIndex;

    private readonly struct SnapshotRegistration
    {
        public SettlementClaimantDecision Decision { get; }
        public IReadOnlyList<SettlementClaimantCandidate> Candidates { get; }

        public SnapshotRegistration(
            SettlementClaimantDecision decision,
            IReadOnlyList<SettlementClaimantCandidate> candidates)
        {
            Decision = decision;
            Candidates = candidates;
        }
    }

    private sealed class Snapshot
    {
        public IReadOnlyList<SettlementClaimantCandidate> Candidates { get; }
        public bool IsSynchronized { get; set; }

        public Snapshot(
            IReadOnlyList<SettlementClaimantCandidate> candidates,
            bool isSynchronized)
        {
            Candidates = candidates;
            IsSynchronized = isSynchronized;
        }
    }
}
