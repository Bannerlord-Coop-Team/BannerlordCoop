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
    private readonly HashSet<string> loggedJoinBaselineFailures = new HashSet<string>();
    private string lastJoinBaselineFailure;

    internal string LastJoinBaselineFailure => lastJoinBaselineFailure;
    internal int LoggedJoinBaselineFailureCount => loggedJoinBaselineFailures.Count;

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
        if (!TryGetInteractableReference(
            party.Ai.AiBehaviorInteractable,
            out string interactablePointId,
            out bool isInteractableAnchor))
        {
            return FailCreation(
                $"AI interactable '{party.Ai.AiBehaviorInteractable?.GetType().Name}' is not registered",
                out failure);
        }
        if (!TryGetCompactId(party.TargetParty, out string targetPartyId))
        {
            return FailCreation(
                $"target party '{party.TargetParty?.StringId}' is not registered",
                out failure);
        }
        if (!TryGetCompactId(party.TargetSettlement, out string targetSettlementId))
        {
            return FailCreation(
                $"target settlement '{party.TargetSettlement?.StringId}' is not registered",
                out failure);
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

        PartyBehaviorUpdateData behavior;
        if (TryGetInvalidJoinReferences(
            party,
            liveParties,
            liveSettlements,
            out string invalidReferences))
        {
            if (!TryGetCompactId(party, out string partyId))
                return FailCreation("party is not registered", out failure);

            Logger.Warning(
                "Normalized stale join references ({References}) on party {Party} for join baseline",
                invalidReferences,
                party.StringId);

            // Stale objects cannot be represented on the joining client. Preserve the movement state.
            behavior = party.PartyMoveMode == MoveModeType.Hold
                ? CreateHeldJoinBehavior(party, partyId)
                : CreatePointJoinBehavior(party, partyId);
        }
        else if (!TryCreate(party, out behavior, out failure))
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

    private static PartyBehaviorUpdateData CreatePointJoinBehavior(MobileParty party, string partyId)
    {
        CampaignVec2 destination = party.MoveTargetParty?.Position ?? party.MoveTargetPoint;
        return new PartyBehaviorUpdateData(
            partyId,
            AiBehavior.GoToPoint,
            null,
            destination,
            party.Position,
            AiBehavior.GoToPoint,
            destination,
            party.DesiredAiNavigationType)
        {
            MoveTargetPoint = destination,
            PartyMoveMode = MoveModeType.Point,
            IsCurrentlyAtSea = party.IsCurrentlyAtSea,
        };
    }

    private static PartyBehaviorUpdateData CreateHeldJoinBehavior(MobileParty party, string partyId) =>
        new PartyBehaviorUpdateData(
            partyId,
            AiBehavior.Hold,
            null,
            party.Position,
            party.Position,
            AiBehavior.Hold,
            party.Position,
            MobileParty.NavigationType.None)
        {
            MoveTargetPoint = party.MoveTargetPoint,
            PartyMoveMode = MoveModeType.Hold,
            IsCurrentlyAtSea = party.IsCurrentlyAtSea,
        };

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
        if (!TryPrepare(
            party,
            data,
            null,
            null,
            logLookupFailures: true,
            out ResolvedBehaviorUpdate resolved,
            out _))
            return false;

        interactable = resolved.Interactable;
        ApplyBehavior(resolved, resetPath: false);
        return true;
    }

    public bool TryApplyJoinBaseline(MobilePartyJoinState[] states, Action beforeApply)
    {
        if (states == null)
            return RejectJoinBaseline("the baseline party-state array is null");
        if (beforeApply == null)
            return RejectJoinBaseline("the before-apply callback is null");

        var objectManager = Campaign.Current?.CampaignObjectManager;
        var parties = objectManager?.MobileParties;
        var settlements = objectManager?.Settlements;
        if (parties == null)
            return RejectJoinBaseline("the client campaign mobile-party collection is unavailable");
        if (settlements == null)
            return RejectJoinBaseline("the client campaign settlement collection is unavailable");
        var liveParties = new HashSet<MobileParty>();
        for (int i = 0; i < parties.Count; i++)
        {
            MobileParty party = parties[i];
            if (party?.IsActive == true) liveParties.Add(party);
        }

        if (states.Length != liveParties.Count)
        {
            return RejectJoinBaseline(
                $"party count mismatch (baseline={states.Length}, client={liveParties.Count})");
        }

        var liveSettlements = new HashSet<Settlement>(settlements);
        var seenParties = new HashSet<MobileParty>();
        var resolved = new ResolvedBehaviorUpdate[states.Length];

        for (int i = 0; i < states.Length; i++)
        {
            PartyBehaviorUpdateData behavior = states[i].Behavior;
            if (string.IsNullOrEmpty(behavior.MobilePartyId))
                return RejectJoinBaseline($"state {i} has no mobile-party id");
            if (!this.objectManager.TryGetObject(
                behavior.MobilePartyId,
                out MobileParty party))
            {
                return RejectJoinBaseline(
                    $"state {i} references missing mobile party '{behavior.MobilePartyId}'");
            }
            if (!liveParties.Contains(party))
            {
                return RejectJoinBaseline(
                    $"state {i} party '{behavior.MobilePartyId}' is not in the client campaign collection");
            }
            if (!seenParties.Add(party))
                return RejectJoinBaseline($"state {i} duplicates party '{behavior.MobilePartyId}'");
            if (!TryPrepare(
                party,
                behavior,
                liveParties,
                liveSettlements,
                logLookupFailures: false,
                out resolved[i],
                out string failure))
            {
                return RejectJoinBaseline(
                    $"state {i} party '{behavior.MobilePartyId}' failed validation: {failure}");
            }
        }

        if (seenParties.Count != liveParties.Count)
        {
            return RejectJoinBaseline(
                $"party coverage mismatch (baseline={seenParties.Count}, client={liveParties.Count})");
        }

        try
        {
            beforeApply();
            using (new AllowedThread())
            {
                for (int i = 0; i < resolved.Length; i++)
                {
                    ApplyJoinState(resolved[i].Party, states[i]);
                    ApplyBehavior(resolved[i], resetPath: true);
                }
            }

            lastJoinBaselineFailure = null;
            loggedJoinBaselineFailures.Clear();
            return true;
        }
        catch (Exception ex)
        {
            return RejectJoinBaseline(
                $"application threw {ex.GetType().Name}: {ex.Message}",
                ex);
        }
    }

    private bool RejectJoinBaseline(string failure, Exception exception = null)
    {
        lastJoinBaselineFailure = failure;
        if (!loggedJoinBaselineFailures.Add(failure))
            return false;

        if (exception == null)
        {
            Logger.Warning(
                "Could not apply mobile-party join baseline: {Failure}. Identical retries will not be logged",
                failure);
        }
        else
        {
            Logger.Error(
                exception,
                "Could not apply mobile-party join baseline: {Failure}. Identical retries will not be logged",
                failure);
        }
        return false;
    }

    private bool TryPrepare(
        MobileParty party,
        PartyBehaviorUpdateData data,
        HashSet<MobileParty> liveParties,
        HashSet<Settlement> liveSettlements,
        bool logLookupFailures,
        out ResolvedBehaviorUpdate resolved,
        out string failure)
    {
        resolved = default;
        failure = null;
        if (party == null)
            return FailPreparation("party is unavailable", out failure);
        if (party.Ai == null)
            return FailPreparation("party AI is unavailable", out failure);
        if (!TryResolveInteractable(data, out IInteractablePoint interactable, logLookupFailures))
            return FailPreparation($"interactable '{data.InteractablePointId}' could not be resolved", out failure);
        if (!TryResolve(data.TargetPartyId, out MobileParty targetParty, logLookupFailures))
            return FailPreparation($"target party '{data.TargetPartyId}' could not be resolved", out failure);
        if (!TryResolve(data.TargetSettlementId, out Settlement targetSettlement, logLookupFailures))
            return FailPreparation($"target settlement '{data.TargetSettlementId}' could not be resolved", out failure);
        if (!TryResolve(data.MoveTargetPartyId, out MobileParty moveTargetParty, logLookupFailures))
            return FailPreparation($"move target party '{data.MoveTargetPartyId}' could not be resolved", out failure);

        if (data.PartyMoveMode == MoveModeType.Party && moveTargetParty == null)
            return FailPreparation("party movement mode requires a move target", out failure);

        if (liveParties != null && targetParty != null && !liveParties.Contains(targetParty))
            return FailPreparation($"target party '{data.TargetPartyId}' is not live", out failure);
        if (liveParties != null && moveTargetParty != null && !liveParties.Contains(moveTargetParty))
            return FailPreparation($"move target party '{data.MoveTargetPartyId}' is not live", out failure);
        if (liveParties != null && !IsLiveInteractable(interactable, liveParties, liveSettlements))
            return FailPreparation($"interactable '{data.InteractablePointId}' is not live", out failure);

        if (liveSettlements != null &&
            targetSettlement != null &&
            !liveSettlements.Contains(targetSettlement))
        {
            return FailPreparation($"target settlement '{data.TargetSettlementId}' is not live", out failure);
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

    private static bool FailPreparation(string reason, out string failure)
    {
        failure = reason;
        return false;
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

    private bool TryResolveInteractable(
        PartyBehaviorUpdateData data,
        out IInteractablePoint interactable,
        bool logLookupFailures = true)
    {
        interactable = null;
        if (data.InteractablePointId == null)
            return true;
        if (data.IsInteractableAnchor)
            return TryResolve(data.InteractablePointId, out MobileParty owner, logLookupFailures) &&
                (interactable = owner.Anchor) != null;
        return TryResolve(data.InteractablePointId, out PartyBase partyBase, logLookupFailures) &&
            (interactable = partyBase) != null;
    }

    private bool TryResolve<T>(string id, out T value, bool logLookupFailures = true) where T : class
    {
        value = null;
        return id == null || (logLookupFailures
            ? objectManager.TryGetObjectWithLogging(id, out value)
            : objectManager.TryGetObject(id, out value));
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
