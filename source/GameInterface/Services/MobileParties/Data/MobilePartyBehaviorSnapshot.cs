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
    bool TryCreateJoinState(MobileParty party, out MobilePartyJoinState state);
    bool TryCreateJoinState(MobileParty party, out MobilePartyJoinState state, out string failureReason);
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
        out PartyBehaviorUpdateData data) => TryCreate(party, out data, out _);

    private bool TryCreate(
        MobileParty party,
        out PartyBehaviorUpdateData data,
        out string failureReason)
    {
        data = default;
        failureReason = null;
        if (party?.Ai == null)
        {
            failureReason = party == null ? "party is null" : "party.Ai is null";
            return false;
        }
        if (!TryGetCompactId(party, out string partyId))
        {
            failureReason = "the party itself is not registered with the object manager";
            return false;
        }
        if (!TryGetInteractableReference(
                party.Ai.AiBehaviorInteractable,
                out string interactablePointId,
                out bool isInteractableAnchor))
        {
            failureReason = DescribeStaleInteractable(party.Ai.AiBehaviorInteractable);
            return false;
        }
        if (!TryGetCompactId(party.TargetParty, out string targetPartyId))
        {
            failureReason = DescribeStaleParty("TargetParty", party.TargetParty);
            return false;
        }
        if (!TryGetCompactId(party.TargetSettlement, out string targetSettlementId))
        {
            failureReason = $"TargetSettlement \"{party.TargetSettlement?.StringId}\" is not registered with the object manager";
            return false;
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

    public bool TryCreateJoinState(MobileParty party, out MobilePartyJoinState state) =>
        TryCreateJoinState(party, out state, out _);

    public bool TryCreateJoinState(MobileParty party, out MobilePartyJoinState state, out string failureReason)
    {
        state = default;
        if (!TryCreate(party, out PartyBehaviorUpdateData behavior, out failureReason))
        {
            // A party can hold AI references to objects that no longer exist in the
            // campaign — typically a destroyed party whose PartyBase was persisted
            // into the save as an AI target (#2489: a deserter party kept a dead
            // party as its AiBehaviorInteractable). One such party would otherwise
            // block the join baseline — and therefore every join — forever. The
            // server is authoritative over AI here, so drop the stale references
            // the same way the AI would after its next rethink and capture again.
            if (!TryHealStaleReferences(party, failureReason) ||
                !TryCreate(party, out behavior, out failureReason))
            {
                return false;
            }
        }

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
        return true;
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

    public bool TryApplyJoinBaseline(MobilePartyJoinState[] states, Action beforeApply)
    {
        if (states == null || beforeApply == null) return false;

        var objectManager = Campaign.Current?.CampaignObjectManager;
        var parties = objectManager?.MobileParties;
        var settlements = objectManager?.Settlements;
        if (parties == null || settlements == null || states.Length != parties.Count)
            return false;

        var liveParties = new HashSet<MobileParty>(parties);
        var liveSettlements = new HashSet<Settlement>(settlements);
        var seenParties = new HashSet<MobileParty>();
        var resolved = new ResolvedBehaviorUpdate[states.Length];

        for (int i = 0; i < states.Length; i++)
        {
            PartyBehaviorUpdateData behavior = states[i].Behavior;
            if (string.IsNullOrEmpty(behavior.MobilePartyId) ||
                !this.objectManager.TryGetObjectWithLogging(behavior.MobilePartyId, out MobileParty party) ||
                !liveParties.Contains(party) ||
                !seenParties.Add(party) ||
                !TryPrepare(party, behavior, liveParties, liveSettlements, out resolved[i]))
            {
                return false;
            }
        }

        if (seenParties.Count != liveParties.Count) return false;

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

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply a complete mobile-party join baseline");
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
        HashSet<MobileParty> liveParties,
        HashSet<Settlement> liveSettlements)
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

    /// <summary>
    /// Server-side repair for a party whose AI references cannot be resolved to
    /// synced ids (the referenced object is gone from the campaign). Clears only
    /// the stale members — targets first, then the behaviors that depended on
    /// them, mirroring <see cref="ApplyBehavior"/>'s ordering — and leaves the
    /// party holding position until the server AI's next rethink re-tasks it.
    /// Returns true when something was repaired so the capture can be retried.
    /// </summary>
    private bool TryHealStaleReferences(MobileParty party, string failureReason)
    {
        if (party?.Ai == null)
            return false;

        var healed = new List<string>();

        if (!TryGetCompactId(party.TargetParty, out _))
        {
            party.TargetParty = null;
            if (party.DefaultBehavior == AiBehavior.EngageParty ||
                party.DefaultBehavior == AiBehavior.EscortParty)
            {
                party.DefaultBehavior = AiBehavior.Hold;
            }
            healed.Add(nameof(party.TargetParty));
        }

        if (!TryGetCompactId(party.TargetSettlement, out _))
        {
            party.SetTargetSettlement(null, false);
            if (party.DefaultBehavior == AiBehavior.GoToSettlement ||
                party.DefaultBehavior == AiBehavior.RaidSettlement ||
                party.DefaultBehavior == AiBehavior.BesiegeSettlement ||
                party.DefaultBehavior == AiBehavior.DefendSettlement)
            {
                party.DefaultBehavior = AiBehavior.Hold;
            }
            healed.Add(nameof(party.TargetSettlement));
        }

        if (!TryGetInteractableReference(party.Ai.AiBehaviorInteractable, out _, out _))
        {
            party.SetShortTermBehavior(AiBehavior.Hold, null);
            healed.Add(nameof(party.Ai.AiBehaviorInteractable));
        }

        if (healed.Count == 0)
            return false;

        Logger.Warning(
            "Cleared stale AI references ({Members}) on party {Party} so it can join-sync; original failure: {Reason}",
            string.Join(", ", healed), party.StringId, failureReason);
        return true;
    }

    /// <summary>
    /// Failure detail for the join-baseline path: identifies which reference on a
    /// party could not be resolved to a synced id and whether the referenced object
    /// is still part of the live campaign, so a single log line localizes the bug.
    /// </summary>
    private static string DescribeStaleParty(string member, MobileParty target)
    {
        if (target == null) return $"{member} is null but was reported unresolvable";
        bool inCampaign = Campaign.Current?.CampaignObjectManager?.MobileParties?.Contains(target) ?? false;
        return $"{member} \"{target.StringId}\" is not registered with the object manager " +
            $"(IsActive={target.IsActive}, inCampaignList={inCampaign}, mapEvent={target.MapEvent != null}, " +
            $"type={target.PartyComponent?.GetType().Name ?? "<none>"})";
    }

    private static string DescribeStaleInteractable(IInteractablePoint interactable)
    {
        if (interactable is AnchorPoint anchor)
        {
            return anchor.Owner == null
                ? "AiBehaviorInteractable is an AnchorPoint with a null Owner"
                : DescribeStaleParty("AiBehaviorInteractable(anchor.Owner)", anchor.Owner);
        }
        if (interactable is PartyBase partyBase)
        {
            if (partyBase.MobileParty != null)
                return DescribeStaleParty("AiBehaviorInteractable(PartyBase.MobileParty)", partyBase.MobileParty);
            return $"AiBehaviorInteractable PartyBase (settlement \"{partyBase.Settlement?.StringId ?? "<null>"}\") is not registered with the object manager";
        }
        return $"AiBehaviorInteractable of type {interactable?.GetType().Name ?? "<null>"} could not be referenced";
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
