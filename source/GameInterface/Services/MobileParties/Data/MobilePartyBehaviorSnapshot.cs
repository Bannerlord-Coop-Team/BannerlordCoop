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
    bool TryCreateJoinState(MobileParty party, out MobilePartyJoinState state);
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
        out PartyBehaviorUpdateData data)
    {
        data = default;
        if (party?.Ai == null)
            return false;
        if (!TryGetCompactId(party, out string partyId) ||
            !TryGetInteractableReference(
                party.Ai.AiBehaviorInteractable,
                out string interactablePointId,
                out bool isInteractableAnchor) ||
            !TryGetCompactId(party.TargetParty, out string targetPartyId) ||
            !TryGetCompactId(party.TargetSettlement, out string targetSettlementId) ||
            !TryGetCompactId(party.MoveTargetParty, out string moveTargetPartyId))
            return false;
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
            MoveTargetPoint = party.MoveTargetPoint,
            IsTargetingPort = party.IsTargetingPort,
            PartyMoveMode = party.PartyMoveMode,
            MoveTargetPartyId = moveTargetPartyId,
            IsInteractableAnchor = isInteractableAnchor,
        };
        return true;
    }

    public bool TryCreateJoinState(MobileParty party, out MobilePartyJoinState state)
    {
        state = default;
        if (!TryCreate(party, out PartyBehaviorUpdateData behavior))
            return false;

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
