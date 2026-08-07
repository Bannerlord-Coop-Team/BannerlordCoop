using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.MobileParties.Data;

public interface IMobilePartyBehaviorSnapshot
{
    bool TryCreate(MobileParty party, out PartyBehaviorUpdateData data);
    bool CanApply(MobileParty party, PartyBehaviorUpdateData data);
    bool TryCreateJoinState(
        MobileParty party,
        ISet<MobileParty> liveParties,
        ISet<Settlement> liveSettlements,
        out MobilePartyJoinState state,
        out string failure);
    bool TryApply(MobileParty party, PartyBehaviorUpdateData data, out IInteractablePoint interactable);
    bool TryApplyJoinBaseline(MobilePartyJoinState[] states, Action beforeApply);
}

public sealed class MobilePartyBehaviorSnapshot : IMobilePartyBehaviorSnapshot
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyBehaviorSnapshot>();

    private readonly IObjectManager objectManager;

    public MobilePartyBehaviorSnapshot(IObjectManager objectManager) => this.objectManager = objectManager;

    public bool TryCreate(
        MobileParty party,
        out PartyBehaviorUpdateData data) =>
        TryCreate(party, out data, out _);

    private bool TryCreate(
        MobileParty party,
        out PartyBehaviorUpdateData data,
        out string failure)
    {
        data = default;
        failure = null;
        if (party == null)
            return FailCreation("party is null", out failure);
        if (party.Ai == null)
            return FailCreation("party AI is unavailable", out failure);
        if (!TryGetCompactId(party, out string partyId))
            return FailCreation("party is not registered", out failure);
        // An unresolvable REFERENCE must not abandon the whole update. These three used to return false, which
        // meant no behaviour was ever produced for that party - and because the sync only ever sends behaviour
        // this way, the client never learned the party's ShortTermBehavior and left it at None. Such a party
        // shows the DefaultBehavior it was given ("patrolling", or nothing the UI can name) and never acts on
        // it, permanently, because the missing reference never comes back.
        //
        // Seen live after a battle wedged and took its parties down with it: ~850 parties still pointed at
        // destroyed ones, so 850 updates were dropped in silence - FailCreation discards its reason - leaving
        // the map frozen on every client while the server ran on happily. Dropping the reference and keeping
        // the behaviour is strictly better: the client gets a party that moves, and the AI re-targets on its
        // next decision. This is what the MoveTargetParty branch below has always done.
        if (!TryGetInteractableReference(
            party.Ai.AiBehaviorInteractable,
            out string interactablePointId,
            out bool isInteractableAnchor))
        {
            WarnDroppedReference(party, "AI interactable", party.Ai.AiBehaviorInteractable?.GetType().Name);
            interactablePointId = null;
            isInteractableAnchor = false;
        }
        if (!TryGetCompactId(party.TargetParty, out string targetPartyId))
        {
            WarnDroppedReference(party, "target party", party.TargetParty?.StringId);
            targetPartyId = null;
        }
        if (!TryGetCompactId(party.TargetSettlement, out string targetSettlementId))
        {
            WarnDroppedReference(party, "target settlement", party.TargetSettlement?.StringId);
            targetSettlementId = null;
        }

        MoveModeType partyMoveMode = party.PartyMoveMode;
        CampaignVec2 moveTargetPoint = party.MoveTargetPoint;
        MobileParty moveTargetParty = party.MoveTargetParty;
        if (!TryGetCompactId(moveTargetParty, out string moveTargetPartyId))
        {
            // A removed movement target cannot exist on clients, so preserve its last destination.
            moveTargetPartyId = null;
            if (partyMoveMode == MoveModeType.Party)
            {
                partyMoveMode = MoveModeType.Point;
                moveTargetPoint = moveTargetParty.Position;
            }
        }

        data = new PartyBehaviorUpdateData(
            partyId,
            party.ShortTermBehavior,
            interactablePointId,
            party.Ai.BehaviorTarget,
            party.Position,
            party.DefaultBehavior,
            party.TargetPosition,
            party.DesiredAiNavigationType)
        {
            TargetPartyId = targetPartyId,
            TargetSettlementId = targetSettlementId,
            MoveTargetPoint = moveTargetPoint,
            IsTargetingPort = party.IsTargetingPort,
            PartyMoveMode = partyMoveMode,
            MoveTargetPartyId = moveTargetPartyId,
            IsInteractableAnchor = isInteractableAnchor,
            IsCurrentlyAtSea = party.IsCurrentlyAtSea,
        };
        return true;
    }

    public bool CanApply(MobileParty party, PartyBehaviorUpdateData data) =>
        party?.Ai != null &&
        TryResolveInteractable(data, out _) &&
        TryResolve(data.TargetPartyId, out MobileParty _) &&
        TryResolve(data.TargetSettlementId, out Settlement _) &&
        TryResolve(data.MoveTargetPartyId, out MobileParty _);

    public bool TryCreateJoinState(
        MobileParty party,
        ISet<MobileParty> liveParties,
        ISet<Settlement> liveSettlements,
        out MobilePartyJoinState state,
        out string failure)
    {
        state = default;
        if (liveParties == null || liveSettlements == null)
            return FailCreation("live campaign objects are unavailable", out failure);

        bool created = TryCreate(party, out PartyBehaviorUpdateData behavior, out failure);
        if (TryGetInvalidJoinReferences(
            party,
            liveParties,
            liveSettlements,
            out string invalidReferences))
        {
            if (!TryGetCompactId(party, out _))
                return false;

            try
            {
                // Mirror vanilla's removed-target cleanup so behavior and navigation stay coherent.
                party.SetMoveModeHold();
                party.SetNavigationModeHold();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to reset stale join references on party {Party}", party.StringId);
                return FailCreation(
                    $"failed to reset stale join references ({invalidReferences}): " +
                    $"{ex.GetType().Name}: {ex.Message}",
                    out failure);
            }

            Logger.Warning(
                "Reset stale join references ({References}) on party {Party} to Hold",
                invalidReferences,
                party.StringId);

            if (!TryCreate(party, out behavior, out failure))
                return false;
            if (TryGetInvalidJoinReferences(
                party,
                liveParties,
                liveSettlements,
                out string remainingReferences))
            {
                return FailCreation(
                    $"stale join references remain after reset ({remainingReferences})",
                    out failure);
            }
        }
        else if (!created)
        {
            return false;
        }

        // Preserve a removed move target as its last point instead of resetting the party to Hold.
        PreserveUnavailableMoveTarget(party, liveParties, ref behavior);

        state = new MobilePartyJoinState
        {
            Behavior = behavior,
            EventPositionAdder = party.EventPositionAdder,
            ArmyPositionAdder = party.ArmyPositionAdder,
            Bearing = party.Bearing,
            IsCurrentlyAtSea = party.IsCurrentlyAtSea,
            EndPositionForNavigationTransition = party.EndPositionForNavigationTransition,
            NavigationTransitionStartTimeTicks = party.NavigationTransitionStartTime.NumTicks,
            StartTransitionNextFrameToExitFromPort = party.StartTransitionNextFrameToExitFromPort,
            ForceAiNoPathMode = party.ForceAiNoPathMode,
        };
        failure = null;
        return true;
    }

    // Once per party per reference kind: a stuck party would otherwise repeat this every tick, and the point
    // is to make a previously SILENT drop visible, not to trade one log flood for another.
    private static readonly HashSet<string> warnedDrops = new HashSet<string>();

    private static void WarnDroppedReference(MobileParty party, string what, string referenced)
    {
        var key = party.StringId + "|" + what;
        lock (warnedDrops)
        {
            if (!warnedDrops.Add(key)) return;
        }

        Logger.Warning(
            "[PartySync] {Party} references an unregistered {What} ('{Referenced}'); dropping that reference and " +
            "syncing the behaviour without it, rather than sending nothing for this party",
            party.StringId, what, referenced ?? "<null>");
    }

    private bool TryGetInvalidJoinReferences(
        MobileParty party,
        ISet<MobileParty> liveParties,
        ISet<Settlement> liveSettlements,
        out string references)
    {
        references = null;
        if (party?.Ai == null) return false;

        var invalidReferences = new List<string>();
        IInteractablePoint interactable = party.Ai.AiBehaviorInteractable;
        if (interactable != null &&
            (!TryGetInteractableReference(interactable, out _, out _) ||
             !IsLiveInteractable(interactable, liveParties, liveSettlements)))
        {
            invalidReferences.Add(nameof(MobilePartyAi.AiBehaviorInteractable));
        }
        if (IsInvalidJoinReference(party.TargetParty, liveParties))
            invalidReferences.Add(nameof(MobileParty.TargetParty));
        if (IsInvalidJoinReference(party.TargetSettlement, liveSettlements))
            invalidReferences.Add(nameof(MobileParty.TargetSettlement));

        if (invalidReferences.Count == 0) return false;

        references = string.Join(", ", invalidReferences);
        return true;
    }

    private static void PreserveUnavailableMoveTarget(
        MobileParty party,
        ISet<MobileParty> liveParties,
        ref PartyBehaviorUpdateData behavior)
    {
        MobileParty moveTargetParty = party.MoveTargetParty;
        if (moveTargetParty == null || liveParties.Contains(moveTargetParty)) return;

        behavior.MoveTargetPartyId = null;
        if (behavior.PartyMoveMode != MoveModeType.Party) return;

        behavior.PartyMoveMode = MoveModeType.Point;
        behavior.MoveTargetPoint = moveTargetParty.Position;
    }

    private bool IsInvalidJoinReference<T>(T instance, ISet<T> liveObjects)
        where T : class =>
        instance != null &&
        (!TryGetCompactId(instance, out _) || !liveObjects.Contains(instance));

    private static bool FailCreation(string reason, out string failure)
    {
        failure = reason;
        return false;
    }

    public bool TryApply(MobileParty party, PartyBehaviorUpdateData data, out IInteractablePoint interactable)
    {
        interactable = null;
        if (!TryPrepare(party, data, null, null, out ResolvedBehaviorUpdate resolved))
            return false;

        interactable = resolved.Interactable;
        ApplyBehavior(resolved, resetPath: false);
        return true;
    }

    /// <summary>
    /// Applies as much of a joining client's authoritative party baseline as will resolve.
    /// </summary>
    /// <remarks>
    /// Deliberately best-effort, and it was not always so. This used to be all-or-nothing: a single party that
    /// failed to resolve, or a client whose party count differed from the server's by even one, threw the
    /// ENTIRE baseline away and returned false without logging a word.
    ///
    /// That is a far worse outcome than it looks, because the baseline is the only way a client ever learns the
    /// behaviour of a party whose orders do not subsequently change. Ordinary updates are published on CHANGE,
    /// so a lord already patrolling when the client joined generates nothing to send. Lose the baseline and
    /// that party sits on the client with ShortTermBehavior = None forever: it displays the DefaultBehavior it
    /// was given and never acts on it. Live, that stranded roughly 650 parties while the ~250 whose orders
    /// happened to change afterwards moved normally - which reads, correctly, as "everything on the map is
    /// frozen, but the ones that came out of settlements are fine".
    ///
    /// So: apply every party that resolves, count the ones that do not, and say so. A partial baseline leaves a
    /// few parties waiting for their next change; no baseline leaves all of them stuck for good.
    /// </remarks>
    public bool TryApplyJoinBaseline(MobilePartyJoinState[] states, Action beforeApply)
    {
        if (states == null || beforeApply == null) return false;

        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        var parties = campaignObjectManager?.MobileParties;
        var settlements = campaignObjectManager?.Settlements;
        if (parties == null || settlements == null) return false;

        var liveParties = new HashSet<MobileParty>(parties);
        var liveSettlements = new HashSet<Settlement>(settlements);
        var seenParties = new HashSet<MobileParty>();
        var resolvedUpdates = new List<ResolvedBehaviorUpdate>(states.Length);
        var resolvedStates = new List<MobilePartyJoinState>(states.Length);
        int skipped = 0;

        for (int i = 0; i < states.Length; i++)
        {
            PartyBehaviorUpdateData behavior = states[i].Behavior;
            if (string.IsNullOrEmpty(behavior.MobilePartyId) ||
                !this.objectManager.TryGetObject(behavior.MobilePartyId, out MobileParty party) ||
                !liveParties.Contains(party) ||
                !seenParties.Add(party) ||
                !TryPrepare(party, behavior, liveParties, liveSettlements, out ResolvedBehaviorUpdate prepared))
            {
                skipped++;
                continue;
            }

            resolvedUpdates.Add(prepared);
            resolvedStates.Add(states[i]);
        }

        if (resolvedUpdates.Count == 0)
        {
            Logger.Error(
                "[PartySync] Join baseline resolved none of its {Total} party state(s); this client can then only " +
                "learn a party's behaviour if it later changes, so parties with standing orders will never move",
                states.Length);
            return false;
        }

        try
        {
            beforeApply();
            using (new AllowedThread())
            {
                for (int i = 0; i < resolvedUpdates.Count; i++)
                {
                    ApplyJoinState(resolvedUpdates[i].Party, resolvedStates[i]);
                    ApplyBehavior(resolvedUpdates[i], resetPath: true);
                }
            }

            if (skipped > 0)
            {
                Logger.Warning(
                    "[PartySync] Join baseline applied to {Applied} of {Total} party state(s); {Skipped} did not " +
                    "resolve and will stay still until their orders next change",
                    resolvedUpdates.Count, states.Length, skipped);
            }
            else
            {
                Logger.Information(
                    "[PartySync] Join baseline applied to all {Applied} party state(s)", resolvedUpdates.Count);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply a mobile-party join baseline");
            return false;
        }
    }

    private bool TryPrepare(
        MobileParty party,
        PartyBehaviorUpdateData data,
        HashSet<MobileParty> liveParties,
        HashSet<Settlement> liveSettlements,
        out ResolvedBehaviorUpdate resolved)
    {
        resolved = default;
        if (party?.Ai == null ||
            !TryResolveInteractable(data, out IInteractablePoint interactable) ||
            !TryResolve(data.TargetPartyId, out MobileParty targetParty) ||
            !TryResolve(data.TargetSettlementId, out Settlement targetSettlement) ||
            !TryResolve(data.MoveTargetPartyId, out MobileParty moveTargetParty))
        {
            return false;
        }

        if (data.PartyMoveMode == MoveModeType.Party && moveTargetParty == null)
            return false;

        if (liveParties != null &&
            ((targetParty != null && !liveParties.Contains(targetParty)) ||
             (moveTargetParty != null && !liveParties.Contains(moveTargetParty)) ||
             !IsLiveInteractable(interactable, liveParties, liveSettlements)))
        {
            return false;
        }

        if (liveSettlements != null &&
            targetSettlement != null &&
            !liveSettlements.Contains(targetSettlement))
        {
            return false;
        }

        resolved = new ResolvedBehaviorUpdate(
            party,
            data,
            interactable,
            targetParty,
            targetSettlement,
            moveTargetParty);
        return true;
    }

    private static bool IsLiveInteractable(
        IInteractablePoint interactable,
        ISet<MobileParty> liveParties,
        ISet<Settlement> liveSettlements)
    {
        if (interactable == null) return true;
        if (interactable is AnchorPoint anchor)
            return anchor.Owner != null && liveParties.Contains(anchor.Owner);
        if (interactable is PartyBase partyBase)
        {
            if (partyBase.MobileParty != null) return liveParties.Contains(partyBase.MobileParty);
            if (partyBase.Settlement != null) return liveSettlements.Contains(partyBase.Settlement);
        }
        return false;
    }

    private static void ApplyJoinState(MobileParty party, MobilePartyJoinState state)
    {
        party.IsCurrentlyAtSea = state.IsCurrentlyAtSea;
        party.Position = state.Behavior.PartyPosition;
        party.EventPositionAdder = state.EventPositionAdder;
        party.ArmyPositionAdder = state.ArmyPositionAdder;
        party.Bearing = state.Bearing;
        party.EndPositionForNavigationTransition = state.EndPositionForNavigationTransition;
        party.NavigationTransitionStartTime = new CampaignTime(state.NavigationTransitionStartTimeTicks);
        party.StartTransitionNextFrameToExitFromPort = state.StartTransitionNextFrameToExitFromPort;
        party.ForceAiNoPathMode = state.ForceAiNoPathMode;
    }

    private static void ApplyBehavior(ResolvedBehaviorUpdate resolved, bool resetPath)
    {
        MobileParty party = resolved.Party;
        PartyBehaviorUpdateData data = resolved.Data;

        // Install targets first because DefaultBehavior can immediately recalculate short-term state.
        party.SetTargetSettlement(resolved.TargetSettlement, data.IsTargetingPort);
        party.TargetParty = resolved.TargetParty;
        party.TargetPosition = data.TargetPosition;
        party.DefaultBehavior = data.DefaultBehavior;
        party.SetShortTermBehavior(data.NewAiBehavior, resolved.Interactable);
        party.DesiredAiNavigationType = data.DesiredAiNavigationType;
        party.Ai.BehaviorTarget = data.BestTargetPoint;
        party.Ai.UpdateBehavior();
        switch (data.PartyMoveMode)
        {
            case MoveModeType.Hold:
                party.SetNavigationModeHold();
                break;
            case MoveModeType.Point:
                party.SetNavigationModePoint(data.MoveTargetPoint);
                break;
            case MoveModeType.Party:
                party.SetNavigationModeParty(resolved.MoveTargetParty);
                break;
            default:
                party.PartyMoveMode = data.PartyMoveMode;
                party.MoveTargetParty = resolved.MoveTargetParty;
                break;
        }
        party.MoveTargetPoint = data.MoveTargetPoint;

        if (resetPath)
        {
            party._pathMode = false;
            party._aiPathNotFound = false;
            party.PathLastFace = PathFaceRecord.NullFaceRecord;
            party.PathBegin = 0;
            party.NextTargetPosition = party.Position;
            party.Party.SetVisualAsDirty();
        }
    }

    private bool TryResolveInteractable(PartyBehaviorUpdateData data, out IInteractablePoint interactable)
    {
        interactable = null;
        if (data.InteractablePointId == null)
            return true;
        if (data.IsInteractableAnchor)
            return TryResolve(data.InteractablePointId, out MobileParty owner) &&
                (interactable = owner.Anchor) != null;
        return TryResolve(data.InteractablePointId, out PartyBase partyBase) &&
            (interactable = partyBase) != null;
    }

    private bool TryResolve<T>(string id, out T value) where T : class
    {
        value = null;
        return id == null || objectManager.TryGetObjectWithLogging(id, out value);
    }

    private bool TryGetInteractableReference(IInteractablePoint interactable, out string id, out bool isAnchor)
    {
        isAnchor = interactable is AnchorPoint;
        if (interactable is PartyBase partyBase)
            return TryGetCompactId(partyBase, out id);
        if (interactable is AnchorPoint anchor && anchor.Owner != null)
            return TryGetCompactId(anchor.Owner, out id);
        id = null;
        return interactable == null;
    }

    private bool TryGetCompactId<T>(T instance, out string id)
        where T : class
    {
        if (instance != null && objectManager.TryGetId(instance, out id))
        {
            id = Compact(id, typeof(T));
            return true;
        }
        id = null;
        return instance == null;
    }

    private readonly struct ResolvedBehaviorUpdate
    {
        public readonly MobileParty Party;
        public readonly PartyBehaviorUpdateData Data;
        public readonly IInteractablePoint Interactable;
        public readonly MobileParty TargetParty;
        public readonly Settlement TargetSettlement;
        public readonly MobileParty MoveTargetParty;

        public ResolvedBehaviorUpdate(
            MobileParty party,
            PartyBehaviorUpdateData data,
            IInteractablePoint interactable,
            MobileParty targetParty,
            Settlement targetSettlement,
            MobileParty moveTargetParty)
        {
            Party = party;
            Data = data;
            Interactable = interactable;
            TargetParty = targetParty;
            TargetSettlement = targetSettlement;
            MoveTargetParty = moveTargetParty;
        }
    }
}
