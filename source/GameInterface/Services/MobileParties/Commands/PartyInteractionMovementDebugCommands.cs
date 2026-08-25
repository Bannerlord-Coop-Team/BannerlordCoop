using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MobileParties.Commands;

internal static class PartyInteractionMovementDebugCommands
{
    private static CaravanProximityFixtureState caravanProximityFixture;
    private static CaravanProximityFixtureState restoredCaravanProximityFixture;

    [CommandLineArgumentFunction("movement_state", "coop.debug.mobileparty")]
    public static string MovementState(List<string> args)
    {
        if (args.Count == 0)
            return "Usage: coop.debug.mobileparty.movement_state <MobilePartyId> [MobilePartyId...]";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";

        var parties = new List<object>(args.Count);
        int holdCount = 0;
        foreach (string id in args)
        {
            if (!TryFindParty(objectManager, id, out MobileParty party))
                return $"Mobile party '{id}' was not found.";

            if (IsHeld(party)) holdCount++;
            parties.Add(DescribeParty(objectManager, party));
        }

        Hero mainHero = ModInformation.IsClient ? Hero.MainHero : null;
        string mainHeroRegistryId = null;
        bool mainHeroRegistered = mainHero != null && objectManager.TryGetId(mainHero, out mainHeroRegistryId);
        MobileParty mainParty = ModInformation.IsClient ? MobileParty.MainParty : null;
        bool mainPartyAvailable = mainParty?.IsActive == true && mainParty.Party != null &&
            mainParty.CurrentSettlement == null && mainParty.MapEvent == null;
        IInteractablePoint mainPartyInteractable = mainParty?.Ai?.AiBehaviorInteractable;
        MobileParty mainPartyInteractionTarget = GetInteractableParty(mainPartyInteractable);
        string mainPartyInteractablePointId = GetInteractablePointId(objectManager, mainPartyInteractable);
        CampaignVec2? mainPartyInteractionPosition = GetInteractionPosition(mainPartyInteractable, mainParty);
        MobileParty nearestCaravan = mainPartyAvailable == false
            ? null
            : MobileParty.All
                .Where(party => party?.IsActive == true && party.IsCaravan &&
                    party.Party != null && party.CurrentSettlement == null && party.MapEvent == null &&
                    party.IsCurrentlyAtSea == mainParty.IsCurrentlyAtSea)
                .OrderBy(party => DistanceSquared(party, mainParty))
                .FirstOrDefault();
        MobileParty nearestInteractableCaravan = mainPartyAvailable == false
            ? null
            : MobileParty.All
                .Where(party => party?.IsActive == true && party.IsCaravan &&
                    party.Party != null && party.CurrentSettlement == null && party.MapEvent == null &&
                    party.IsCurrentlyAtSea == mainParty.IsCurrentlyAtSea &&
                    CanInteractWithParty(party, mainParty))
                .OrderBy(party => DistanceSquared(party, mainParty))
                .FirstOrDefault();

        string nearestCaravanId = null;
        float? nearestCaravanDistance = null;
        if (nearestCaravan != null)
        {
            nearestCaravanId = GetPartyId(objectManager, nearestCaravan);
            nearestCaravanDistance = Distance(nearestCaravan, mainParty);
        }
        string nearestInteractableCaravanId = null;
        float? nearestInteractableCaravanDistance = null;
        if (nearestInteractableCaravan != null)
        {
            nearestInteractableCaravanId = GetPartyId(objectManager, nearestInteractableCaravan);
            nearestInteractableCaravanDistance = Distance(nearestInteractableCaravan, mainParty);
        }

        return JsonResult(new
        {
            role = ModInformation.IsServer ? "server" : "client",
            timeControlMode = Campaign.Current.TimeControlMode.ToString(),
            menuId = Campaign.Current.CurrentMenuContext?.GameMenu?.StringId,
            hasPlayerEncounter = PlayerEncounter.Current != null,
            mainPartyAvailable,
            mainHeroId = mainHero?.StringId,
            mainHeroRegistryId,
            mainHeroRegistered,
            mainPartyId = GetPartyId(objectManager, mainParty),
            mainPartyStringId = mainParty?.StringId,
            mainPartyIsActive = mainParty?.IsActive,
            mainPartyHasPartyBase = mainParty?.Party != null,
            mainPartySettlementId = mainParty?.CurrentSettlement?.StringId,
            mainPartyMapEventId = mainParty?.MapEvent?.StringId,
            mainPartyPositionX = mainParty?.Position.X,
            mainPartyPositionY = mainParty?.Position.Y,
            mainPartyDefaultBehavior = mainParty?.DefaultBehavior.ToString(),
            mainPartyShortTermBehavior = mainParty?.ShortTermBehavior.ToString(),
            mainPartyMoveMode = mainParty?.PartyMoveMode.ToString(),
            mainPartyTargetPartyId = GetPartyId(objectManager, mainParty?.TargetParty),
            mainPartyInteractablePointId,
            mainPartyInteractionTargetPartyId = GetPartyId(objectManager, mainPartyInteractionTarget),
            mainPartyInteractionPositionX = mainPartyInteractionPosition?.X,
            mainPartyInteractionPositionY = mainPartyInteractionPosition?.Y,
            mainPartyInteractionRangeVerified = mainPartyInteractionTarget != null &&
                CanInteractWithParty(mainPartyInteractionTarget, mainParty),
            parties,
            holdCount,
            nearestCaravanId,
            nearestCaravanDistance,
            nearestInteractableCaravanId,
            nearestInteractableCaravanDistance
        });
    }

    [CommandLineArgumentFunction("interact_caravan", "coop.debug.mobileparty")]
    public static string InteractCaravan(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Command can only be run on a client.";
        if (args.Count != 1)
            return "Usage: coop.debug.mobileparty.interact_caravan <MobilePartyId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";
        if (!TryFindParty(objectManager, args[0], out MobileParty targetParty))
            return $"Mobile party '{args[0]}' was not found.";
        if (!targetParty.IsCaravan || targetParty.IsActive == false ||
            targetParty.Party == null || targetParty.CurrentSettlement != null || targetParty.MapEvent != null)
            return $"Mobile party '{args[0]}' is not an active caravan on the campaign map.";

        MobileParty mainParty = MobileParty.MainParty;
        if (mainParty?.IsActive != true || mainParty.Party == null ||
            mainParty.CurrentSettlement != null || mainParty.MapEvent != null)
            return "The client player party is not available on the campaign map.";
        if (PlayerEncounter.Current != null)
            return "The client already has an active player encounter.";

        return InteractWithCaravan(objectManager, targetParty, mainParty);
    }

    [CommandLineArgumentFunction("interact_nearest_caravan", "coop.debug.mobileparty")]
    public static string InteractNearestCaravan(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Command can only be run on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.interact_nearest_caravan";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";

        MobileParty mainParty = MobileParty.MainParty;
        if (mainParty?.IsActive != true || mainParty.Party == null ||
            mainParty.CurrentSettlement != null || mainParty.MapEvent != null)
            return "The client player party is not available on the campaign map.";
        if (PlayerEncounter.Current != null)
            return "The client already has an active player encounter.";

        MobileParty targetParty = MobileParty.All
            .Where(party => party?.IsActive == true && party.IsCaravan &&
                party.Party != null && party.CurrentSettlement == null && party.MapEvent == null &&
                party.IsCurrentlyAtSea == mainParty.IsCurrentlyAtSea &&
                CanInteractWithParty(party, mainParty))
            .OrderBy(party => DistanceSquared(party, mainParty))
            .FirstOrDefault();
        if (targetParty == null)
            return "No active caravan is within the vanilla interaction range.";

        return InteractWithCaravan(objectManager, targetParty, mainParty);
    }

    [CommandLineArgumentFunction("caravan_proximity_fixture_capture", "coop.debug.mobileparty")]
    public static string CaptureCaravanProximityFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Command can only be run on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.mobileparty.caravan_proximity_fixture_capture <PlayerMobilePartyId>";
        if (caravanProximityFixture != null)
            return "Caravan proximity fixture is already active; restore it before capturing another.";
        if (restoredCaravanProximityFixture != null)
            return "Caravan proximity fixture restoration must be verified before capturing another.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
        {
            return "Unable to resolve caravan proximity fixture services.";
        }
        if (!TryFindParty(objectManager, args[0], out MobileParty playerParty))
            return $"Player party '{args[0]}' was not found.";
        if (!playerParty.IsPlayerParty())
            return $"Party '{args[0]}' is not registered as a player party.";
        if (!IsAvailableOnMap(playerParty))
            return $"Player party '{args[0]}' must be active on the campaign map and outside a map event, settlement, army, or transition.";
        if (!behaviorSnapshot.TryCreate(playerParty, out PartyBehaviorUpdateData playerState))
            return "Unable to capture the Cemai player-party movement state.";

        MobileParty targetParty = MobileParty.All
            .Where(party => party != playerParty &&
                party?.IsCaravan == true &&
                party.Party is IInteractablePoint &&
                IsAvailableOnMap(party) &&
                party.IsCurrentlyAtSea == playerParty.IsCurrentlyAtSea &&
                objectManager.TryGetId(party, out _) &&
                objectManager.TryGetId(party.Party, out _))
            .OrderBy(party => DistanceSquared(party, playerParty))
            .FirstOrDefault();
        if (targetParty == null)
            return "No registered active caravan is available for the proximity fixture.";

        string playerPartyId = GetPartyId(objectManager, playerParty);
        string targetPartyId = GetPartyId(objectManager, targetParty);
        objectManager.TryGetId(targetParty.Party, out string targetInteractablePointId);
        caravanProximityFixture = new CaravanProximityFixtureState(
            playerParty,
            targetParty,
            playerState,
            playerPartyId,
            targetPartyId,
            targetInteractablePointId,
            playerParty.NextTargetPosition);

        return JsonResult(new
        {
            success = true,
            playerPartyId,
            targetPartyId,
            targetInteractablePointId,
            targetStringId = targetParty.StringId,
            originalDistance = Distance(playerParty, targetParty),
            captured = true
        });
    }

    [CommandLineArgumentFunction("caravan_proximity_fixture_stage", "coop.debug.mobileparty")]
    public static string StageCaravanProximityFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.caravan_proximity_fixture_stage";
        if (caravanProximityFixture == null)
            return "Caravan proximity fixture is not captured.";
        if (caravanProximityFixture.Staged)
            return "Caravan proximity fixture is already staged.";
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve the caravan proximity movement snapshot service.";

        CaravanProximityFixtureState fixture = caravanProximityFixture;
        if (!IsAvailableOnMap(fixture.PlayerParty) || !IsAvailableOnMap(fixture.TargetParty))
            return "The Cemai player party and caravan must remain active on the campaign map.";
        if (fixture.PlayerParty.IsCurrentlyAtSea != fixture.TargetParty.IsCurrentlyAtSea)
            return "The Cemai player party and caravan no longer use the same navigation layer.";

        if (!(fixture.TargetParty.Party is IInteractablePoint targetInteractable))
            return "The captured caravan no longer has a vanilla interaction point.";

        CampaignVec2 interactionPosition = targetInteractable.GetInteractionPosition(fixture.PlayerParty);
        MobilePartyMovementStatePatches.RunWithoutAutomaticBehaviorBroadcast(() =>
        {
            fixture.PlayerParty.SetMoveModeHold();
            fixture.PlayerParty.SetShortTermBehavior(AiBehavior.GoToPoint, targetInteractable);
            fixture.PlayerParty.Ai.BehaviorTarget = interactionPosition;
            fixture.PlayerParty.Position = interactionPosition;
            fixture.PlayerParty.TargetPosition = interactionPosition;
            fixture.PlayerParty.SetNavigationModeHold();
            fixture.PlayerParty.MoveTargetPoint = interactionPosition;
            fixture.PlayerParty.NextTargetPosition = interactionPosition;
        });
        PublishForcedPosition(fixture.PlayerParty);
        fixture.Staged = true;

        bool interactionTargetVerified =
            ReferenceEquals(fixture.PlayerParty.Ai?.AiBehaviorInteractable, fixture.TargetParty.Party);
        bool preCommandEncounterSafe =
            fixture.PlayerParty.DefaultBehavior == AiBehavior.Hold &&
            fixture.PlayerParty.ShortTermBehavior == AiBehavior.GoToPoint &&
            fixture.PlayerParty.PartyMoveMode == MoveModeType.Hold &&
            fixture.PlayerParty.TargetParty == null;
        bool stagedBehaviorSnapshotVerified =
            behaviorSnapshot.TryCreate(fixture.PlayerParty, out PartyBehaviorUpdateData stagedState) &&
            behaviorSnapshot.CanApply(fixture.PlayerParty, stagedState);
        bool interactionRangeVerified = interactionTargetVerified &&
            preCommandEncounterSafe &&
            stagedBehaviorSnapshotVerified &&
            CanInteractWithParty(fixture.TargetParty, fixture.PlayerParty);
        return JsonResult(new
        {
            success = interactionRangeVerified,
            playerPartyId = fixture.PlayerPartyId,
            targetPartyId = fixture.TargetPartyId,
            targetInteractablePointId = fixture.TargetInteractablePointId,
            distance = Distance(fixture.PlayerParty, fixture.TargetParty),
            interactionPositionX = interactionPosition.X,
            interactionPositionY = interactionPosition.Y,
            interactionTargetVerified,
            preCommandEncounterSafe,
            stagedBehaviorSnapshotVerified,
            interactionRangeVerified,
            staged = true
        });
    }

    [CommandLineArgumentFunction("caravan_proximity_fixture_restore", "coop.debug.mobileparty")]
    public static string RestoreCaravanProximityFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.caravan_proximity_fixture_restore";
        if (caravanProximityFixture == null)
            return "Caravan proximity fixture is not active.";
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve the caravan proximity movement snapshot service.";

        CaravanProximityFixtureState restoring = caravanProximityFixture;
        if (!behaviorSnapshot.CanApply(restoring.PlayerParty, restoring.PlayerState) ||
            !RestoreParty(
                behaviorSnapshot,
                restoring.PlayerParty,
                restoring.PlayerState,
                restoring.PlayerNextTargetPosition))
        {
            return JsonResult(new
            {
                success = false,
                playerPartyId = restoring.PlayerPartyId,
                restored = false
            });
        }

        bool restored = TryVerifyRestored(
            behaviorSnapshot,
            restoring.PlayerParty,
            restoring.PlayerState,
            restoring.PlayerNextTargetPosition);
        if (!restored)
        {
            return JsonResult(new
            {
                success = false,
                playerPartyId = restoring.PlayerPartyId,
                restored = false
            });
        }

        caravanProximityFixture = null;
        restoredCaravanProximityFixture = restoring;
        return JsonResult(new
        {
            success = true,
            playerPartyId = restoring.PlayerPartyId,
            restored = true
        });
    }

    [CommandLineArgumentFunction("caravan_proximity_fixture_verify", "coop.debug.mobileparty")]
    public static string VerifyCaravanProximityFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.caravan_proximity_fixture_verify";
        if (restoredCaravanProximityFixture == null)
            return JsonResult(new { success = false, restored = false });
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve the caravan proximity movement snapshot service.";

        CaravanProximityFixtureState restored = restoredCaravanProximityFixture;
        bool verified = TryVerifyRestored(
            behaviorSnapshot,
            restored.PlayerParty,
            restored.PlayerState,
            restored.PlayerNextTargetPosition);
        if (verified)
            restoredCaravanProximityFixture = null;

        return JsonResult(new
        {
            success = verified,
            playerPartyId = restored.PlayerPartyId,
            restored = verified
        });
    }

    [CommandLineArgumentFunction("nearby_lord_parties", "coop.debug.mobileparty")]
    public static string NearbyLordParties(List<string> args)
    {
        if (args.Count < 1 || args.Count > 2)
            return "Usage: coop.debug.mobileparty.nearby_lord_parties <ReferenceMobilePartyId> [MaximumDistance]";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";
        if (!TryFindParty(objectManager, args[0], out MobileParty referenceParty))
            return $"Mobile party '{args[0]}' was not found.";

        float maximumDistance = 100f;
        if (args.Count == 2 &&
            (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out maximumDistance) ||
                maximumDistance <= 0f))
        {
            return "MaximumDistance must be a positive number.";
        }

        var candidates = MobileParty.All
            .Where(party => party?.IsActive == true && party != referenceParty && party.IsLordParty &&
                party.Party != null && party.LeaderHero != null && party.CurrentSettlement == null &&
                party.MapEvent == null && party.IsCurrentlyAtSea == referenceParty.IsCurrentlyAtSea)
            .Select(party => new
            {
                party,
                distance = Distance(party, referenceParty)
            })
            .Where(candidate => candidate.distance <= maximumDistance)
            .OrderBy(candidate => candidate.distance)
            .Select(candidate => new
            {
                distanceFromReference = candidate.distance,
                sameClan = candidate.party.ActualClan == referenceParty.ActualClan,
                leaderHeroId = candidate.party.LeaderHero.StringId,
                leaderName = candidate.party.LeaderHero.Name?.ToString(),
                memberCount = candidate.party.MemberRoster.TotalManCount,
                state = DescribeParty(objectManager, candidate.party)
            })
            .ToList();

        return JsonResult(new
        {
            role = ModInformation.IsServer ? "server" : "client",
            referencePartyId = GetPartyId(objectManager, referenceParty),
            referenceStringId = referenceParty.StringId,
            referenceClanId = referenceParty.ActualClan?.StringId,
            referenceX = referenceParty.Position.X,
            referenceY = referenceParty.Position.Y,
            maximumDistance,
            candidateCount = candidates.Count,
            sameClanCount = candidates.Count(candidate => candidate.sameClan),
            candidates
        });
    }

    [CommandLineArgumentFunction("require_hold_count", "coop.debug.mobileparty")]
    public static string RequireHoldCount(List<string> args)
    {
        if (args.Count < 2 || !int.TryParse(args[0], out int expectedHoldCount))
            return "Usage: coop.debug.mobileparty.require_hold_count <ExpectedCount> <MobilePartyId> [MobilePartyId...]";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";

        var parties = new List<object>(args.Count - 1);
        int actualHoldCount = 0;
        for (int i = 1; i < args.Count; i++)
        {
            if (!TryFindParty(objectManager, args[i], out MobileParty party))
                return $"Mobile party '{args[i]}' was not found.";

            if (IsHeld(party)) actualHoldCount++;
            parties.Add(DescribeParty(objectManager, party));
        }

        if (actualHoldCount != expectedHoldCount)
            return $"Expected {expectedHoldCount} held parties but observed {actualHoldCount}.";

        return JsonResult(new
        {
            expectedHoldCount,
            actualHoldCount,
            parties
        });
    }

    private static object DescribeParty(IObjectManager objectManager, MobileParty party)
    {
        bool registered = objectManager.TryGetId(party, out string registryId);
        MobileParty armyLeader = party.Army?.LeaderParty;
        return new
        {
            partyId = registered ? registryId : party.StringId,
            stringId = party.StringId,
            registered,
            isActive = party.IsActive,
            isLordParty = party.IsLordParty,
            clanId = party.ActualClan?.StringId,
            leaderHeroId = party.LeaderHero?.StringId,
            leaderName = party.LeaderHero?.Name?.ToString(),
            hasPartyBase = party.Party != null,
            memberCount = party.Party?.MemberRoster.TotalManCount,
            hasAi = party.Ai != null,
            isAiDisabled = party.Ai?.IsDisabled,
            doNotMakeNewDecisions = party.Ai?.DoNotMakeNewDecisions,
            attachedToId = GetPartyId(objectManager, party.AttachedTo),
            armyName = party.Army?.Name?.ToString(),
            armyLeaderId = GetPartyId(objectManager, armyLeader),
            isArmyLeader = armyLeader == party,
            currentSettlementId = party.CurrentSettlement?.StringId,
            mapEventId = party.MapEvent?.StringId,
            defaultBehavior = party.DefaultBehavior.ToString(),
            shortTermBehavior = party.ShortTermBehavior.ToString(),
            moveMode = party.PartyMoveMode.ToString(),
            targetPartyId = GetPartyId(objectManager, party.TargetParty),
            targetSettlementId = party.TargetSettlement?.StringId,
            moveTargetPartyId = GetPartyId(objectManager, party.MoveTargetParty),
            moveTargetX = party.MoveTargetPoint.X,
            moveTargetY = party.MoveTargetPoint.Y,
            nextTargetX = party.NextTargetPosition.X,
            nextTargetY = party.NextTargetPosition.Y,
            isHeld = IsHeld(party),
            x = party.Position.X,
            y = party.Position.Y
        };
    }

    private static bool TryFindParty(
        IObjectManager objectManager,
        string id,
        out MobileParty party)
    {
        if (objectManager.TryGetObject(id, out party))
            return true;

        string stringId = id.StartsWith("MobileParty_", StringComparison.Ordinal)
            ? id.Substring("MobileParty_".Length)
            : id;
        party = Campaign.Current.CampaignObjectManager.Find<MobileParty>(stringId);
        return party != null;
    }

    private static string GetPartyId(IObjectManager objectManager, MobileParty party)
    {
        if (party == null)
            return null;

        return objectManager.TryGetId(party, out string id) ? id : party.StringId;
    }

    private static string GetInteractablePointId(
        IObjectManager objectManager,
        IInteractablePoint interactable)
    {
        if (interactable is PartyBase partyBase)
            return objectManager.TryGetId(partyBase, out string id) ? id : null;
        if (interactable is AnchorPoint anchor)
            return objectManager.TryGetId(anchor, out string id) ? id : null;

        return null;
    }

    private static MobileParty GetInteractableParty(IInteractablePoint interactable)
    {
        if (interactable is PartyBase partyBase)
            return partyBase.MobileParty;
        if (interactable is AnchorPoint anchor)
            return anchor.Owner;

        return null;
    }

    private static CampaignVec2? GetInteractionPosition(
        IInteractablePoint interactable,
        MobileParty interactingParty)
    {
        if (interactable == null || interactingParty == null)
            return null;

        return interactable.GetInteractionPosition(interactingParty);
    }

    private static string InteractWithCaravan(
        IObjectManager objectManager,
        MobileParty targetParty,
        MobileParty mainParty)
    {
        if (!CanInteractWithParty(targetParty, mainParty))
            return $"Mobile party '{GetPartyId(objectManager, targetParty)}' is not within the vanilla interaction range.";

        float distance = Distance(targetParty, mainParty);
        ((IInteractablePoint)targetParty.Party).OnPartyInteraction(mainParty);

        return JsonResult(new
        {
            targetPartyId = GetPartyId(objectManager, targetParty),
            targetStringId = targetParty.StringId,
            distance,
            interactionRangeVerified = true,
            interactionAccepted = true,
            hasPlayerEncounter = PlayerEncounter.Current != null,
            menuId = Campaign.Current.CurrentMenuContext?.GameMenu?.StringId
        });
    }

    private static bool CanInteractWithParty(MobileParty targetParty, MobileParty mainParty)
    {
        if (!(targetParty?.Party is IInteractablePoint interactable) || mainParty == null)
            return false;

        try
        {
            return interactable.CanPartyInteract(mainParty, 0f);
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    private static float Distance(MobileParty first, MobileParty second) =>
        (float)Math.Sqrt(DistanceSquared(first, second));

    private static float DistanceSquared(MobileParty first, MobileParty second)
    {
        float x = first.Position.X - second.Position.X;
        float y = first.Position.Y - second.Position.Y;
        return x * x + y * y;
    }

    private static bool IsHeld(MobileParty party) =>
        party.DefaultBehavior == AiBehavior.Hold &&
        party.ShortTermBehavior == AiBehavior.Hold &&
        party.PartyMoveMode == MoveModeType.Hold;

    private static bool IsAvailableOnMap(MobileParty party) =>
        party?.IsActive == true &&
        party.Party != null &&
        party.CurrentSettlement == null &&
        party.MapEvent == null &&
        party.Army == null &&
        !party.IsTransitionInProgress;

    private static bool RestoreParty(
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        MobileParty party,
        PartyBehaviorUpdateData state,
        CampaignVec2 nextTargetPosition)
    {
        bool restored = false;
        MobilePartyMovementStatePatches.RunWithoutAutomaticBehaviorBroadcast(() =>
        {
            restored = behaviorSnapshot.TryApply(party, state, out _);
            if (!restored) return;

            party.Position = state.PartyPosition;
            party.NextTargetPosition = nextTargetPosition;
        });
        if (!restored)
            return false;

        PublishForcedPosition(party);
        return true;
    }

    private static bool TryVerifyRestored(
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        MobileParty party,
        PartyBehaviorUpdateData expected,
        CampaignVec2 expectedNextTargetPosition)
    {
        if (!behaviorSnapshot.TryCreate(party, out PartyBehaviorUpdateData actual))
            return false;

        return actual.MobilePartyId == expected.MobilePartyId &&
            actual.NewAiBehavior == expected.NewAiBehavior &&
            actual.InteractablePointId == expected.InteractablePointId &&
            actual.BestTargetPoint == expected.BestTargetPoint &&
            actual.PartyPosition == expected.PartyPosition &&
            actual.DefaultBehavior == expected.DefaultBehavior &&
            actual.TargetPosition == expected.TargetPosition &&
            actual.DesiredAiNavigationType == expected.DesiredAiNavigationType &&
            actual.TargetPartyId == expected.TargetPartyId &&
            actual.TargetSettlementId == expected.TargetSettlementId &&
            actual.MoveTargetPoint == expected.MoveTargetPoint &&
            actual.IsTargetingPort == expected.IsTargetingPort &&
            actual.PartyMoveMode == expected.PartyMoveMode &&
            actual.MoveTargetPartyId == expected.MoveTargetPartyId &&
            actual.IsInteractableAnchor == expected.IsInteractableAnchor &&
            actual.IsCurrentlyAtSea == expected.IsCurrentlyAtSea &&
            party.NextTargetPosition == expectedNextTargetPosition;
    }

    private static void PublishForcedPosition(MobileParty party) =>
        MessageBroker.Instance.Publish(
            typeof(PartyInteractionMovementDebugCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea));

    private sealed class CaravanProximityFixtureState
    {
        public MobileParty PlayerParty { get; }
        public MobileParty TargetParty { get; }
        public PartyBehaviorUpdateData PlayerState { get; }
        public string PlayerPartyId { get; }
        public string TargetPartyId { get; }
        public string TargetInteractablePointId { get; }
        public CampaignVec2 PlayerNextTargetPosition { get; }
        public bool Staged { get; set; }

        public CaravanProximityFixtureState(
            MobileParty playerParty,
            MobileParty targetParty,
            PartyBehaviorUpdateData playerState,
            string playerPartyId,
            string targetPartyId,
            string targetInteractablePointId,
            CampaignVec2 playerNextTargetPosition)
        {
            PlayerParty = playerParty;
            TargetParty = targetParty;
            PlayerState = playerState;
            PlayerPartyId = playerPartyId;
            TargetPartyId = targetPartyId;
            TargetInteractablePointId = targetInteractablePointId;
            PlayerNextTargetPosition = playerNextTargetPosition;
        }
    }

    private static string JsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);
}
