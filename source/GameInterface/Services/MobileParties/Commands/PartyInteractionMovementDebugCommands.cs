using Common;
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
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MobileParties.Commands;

internal static class PartyInteractionMovementDebugCommands
{
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

    private static bool CanInteractWithParty(MobileParty targetParty, MobileParty mainParty) =>
        targetParty?.Party is IInteractablePoint interactable &&
        mainParty != null &&
        interactable.CanPartyInteract(mainParty, 0f);

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

    private static string JsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);
}
