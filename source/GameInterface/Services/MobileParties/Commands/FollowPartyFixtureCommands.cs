using Common.Commands;
using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using Helpers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
namespace GameInterface.Services.MobileParties.Commands;

internal static class FollowPartyFixtureCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static FixtureState fixture;

    public sealed class SetupCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "follow_fixture_setup";

        public string Description => "Runs fixture setup for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("playerPartyId", "The player party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Command can only be run on the server.");
            if (fixture != null)
                return Failed("Follow fixture is already active; restore it before starting another.");
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
                !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
                return Failed("Unable to resolve fixture services.");
            if (!TryFindParty(args[0], out MobileParty playerParty))
                return Failed($"Player party '{args[0]}' was not found.");
            if (!playerParty.IsPlayerParty())
                return Failed($"Party '{args[0]}' is not registered as a player party.");
            if (!IsAvailableOnMap(playerParty))
                return Failed($"Player party '{args[0]}' must be active on the campaign map and outside a map event, settlement, or army.");

            MobileParty targetParty = MobileParty.All
                .Where(party => party != playerParty &&
                    party.IsLordParty &&
                    !party.IsPlayerParty() &&
                    IsAvailableOnMap(party) &&
                    party.DefaultBehavior == AiBehavior.PatrolAroundPoint &&
                    party.IsCurrentlyAtSea == playerParty.IsCurrentlyAtSea &&
                    objectManager.TryGetId(party, out _))
                .OrderBy(party => party.Position.DistanceSquared(playerParty.Position))
                .FirstOrDefault();
            if (targetParty == null)
                return Failed("No eligible patrolling registered AI lord party is available for the follow fixture.");
            if (!behaviorSnapshot.TryCreate(playerParty, out PartyBehaviorUpdateData playerState) ||
                !behaviorSnapshot.TryCreate(targetParty, out PartyBehaviorUpdateData targetState))
                return Failed("Unable to capture the original movement state for both fixture parties.");

            MobileParty.NavigationType navigationType = playerParty.IsCurrentlyAtSea
                ? MobileParty.NavigationType.Naval
                : MobileParty.NavigationType.Default;
            CampaignVec2 targetPosition = NavigationHelper.FindPointAroundPosition(
                playerParty.Position,
                navigationType,
                8f,
                5f);
            if (!targetPosition.IsValid() || targetPosition == playerParty.Position)
                return Failed("Unable to find a reachable staging point near the player party.");

            fixture = new FixtureState(
                playerParty,
                targetParty,
                playerState,
                targetState);
            StageAtHold(playerParty, playerParty.Position);
            StageAtHold(targetParty, targetPosition);

            return Succeeded($"Follow fixture ready|player={playerParty.StringId}|target={targetParty.StringId}|" +
                $"distance={Distance(playerParty, targetParty).ToString("R", CultureInfo.InvariantCulture)}");

        }
    }

    public sealed class FollowCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "follow_fixture_follow";

        public string Description => "Runs fixture follow for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("targetPartyId", "The target party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient)
                return Failed("Command can only be run on a client.");
            if (!TryFindParty(args[0], out MobileParty targetParty))
                return Failed($"Target party '{args[0]}' was not found.");

            MobileParty playerParty = MobileParty.MainParty;
            if (!IsAvailableOnMap(playerParty) || !IsAvailableOnMap(targetParty))
                return Failed("Both parties must be active on the campaign map.");
            if (playerParty.IsCurrentlyAtSea != targetParty.IsCurrentlyAtSea)
                return Failed("The parties must use the same navigation layer.");

            MobileParty.NavigationType navigationType = playerParty.IsCurrentlyAtSea
                ? MobileParty.NavigationType.Naval
                : MobileParty.NavigationType.Default;
            playerParty.SetMoveEscortParty(targetParty, navigationType, isTargetingPort: false);

            return Succeeded($"Follow command submitted|player={playerParty.StringId}|target={targetParty.StringId}");

        }
    }

    public sealed class MoveTargetCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "follow_fixture_move_target";

        public string Description => "Runs fixture move target for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Command can only be run on the server.");
            if (fixture == null)
                return Failed("Follow fixture is not active.");

            MobileParty.NavigationType navigationType = fixture.TargetParty.IsCurrentlyAtSea
                ? MobileParty.NavigationType.Naval
                : MobileParty.NavigationType.Default;
            CampaignVec2 targetPoint = NavigationHelper.FindPointAroundPosition(
                fixture.PlayerParty.Position,
                navigationType,
                22f,
                18f);
            if (!targetPoint.IsValid() || targetPoint == fixture.TargetParty.Position)
                return Failed("Unable to find a reachable target movement point.");

            fixture.TargetParty.SetNavigationModePoint(targetPoint);
            fixture.TargetParty.SetMoveGoToPoint(targetPoint, navigationType);
            return Succeeded($"AI target movement started|target={fixture.TargetParty.StringId}|" +
                $"x={targetPoint.X.ToString("R", CultureInfo.InvariantCulture)}|" +
                $"y={targetPoint.Y.ToString("R", CultureInfo.InvariantCulture)}");

        }
    }

    public sealed class StateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "follow_fixture_state";

        public string Description => "Runs fixture state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("playerPartyId", "The player party id."),
            new ExpectedArgs("targetPartyId", "The target party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryFindParty(args[0], out MobileParty playerParty))
                return Failed($"Player party '{args[0]}' was not found.");
            if (!TryFindParty(args[1], out MobileParty targetParty))
                return Failed($"Target party '{args[1]}' was not found.");

            return Succeeded($"player={playerParty.StringId}|target={targetParty.StringId}|" +
                $"distance={Distance(playerParty, targetParty).ToString("R", CultureInfo.InvariantCulture)}|" +
                $"playerDefault={playerParty.DefaultBehavior}|playerShort={playerParty.ShortTermBehavior}|" +
                $"playerMoveMode={playerParty.PartyMoveMode}|playerMoving={playerParty.IsMoving}|" +
                $"playerWaiting={playerParty.ComputeIsWaiting()}|" +
                $"playerX={playerParty.Position.X.ToString("R", CultureInfo.InvariantCulture)}|" +
                $"playerY={playerParty.Position.Y.ToString("R", CultureInfo.InvariantCulture)}|" +
                $"targetDefault={targetParty.DefaultBehavior}|targetShort={targetParty.ShortTermBehavior}|" +
                $"targetMoveMode={targetParty.PartyMoveMode}|targetMoving={targetParty.IsMoving}|" +
                $"targetX={targetParty.Position.X.ToString("R", CultureInfo.InvariantCulture)}|" +
                $"targetY={targetParty.Position.Y.ToString("R", CultureInfo.InvariantCulture)}");

        }
    }

    public sealed class RestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobileparty";

        public string Name => "follow_fixture_restore";

        public string Description => "Runs fixture restore for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Command can only be run on the server.");
            if (fixture == null)
                return Failed("Follow fixture is not active.");
            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
                return Failed("Unable to resolve the movement snapshot service.");

            FixtureState restoring = fixture;
            bool targetRestored = RestoreParty(behaviorSnapshot, restoring.TargetParty, restoring.TargetState);
            bool playerRestored = RestoreParty(behaviorSnapshot, restoring.PlayerParty, restoring.PlayerState);
            bool targetVerified = targetRestored &&
                TryVerifyRestored(behaviorSnapshot, restoring.TargetParty, restoring.TargetState);
            bool playerVerified = playerRestored &&
                TryVerifyRestored(behaviorSnapshot, restoring.PlayerParty, restoring.PlayerState);
            if (!targetVerified || !playerVerified)
                return Failed($"Follow fixture restoration failed|player={playerVerified}|target={targetVerified}");

            fixture = null;
            return Succeeded($"Follow fixture restored|player={restoring.PlayerParty.StringId}|" +
                $"target={restoring.TargetParty.StringId}|verified=true");

        }
    }

    private static bool IsAvailableOnMap(MobileParty party) =>
        party?.IsActive == true &&
        party.CurrentSettlement == null &&
        party.MapEvent == null &&
        party.Army == null &&
        !party.IsTransitionInProgress;

    private static bool TryFindParty(string id, out MobileParty party)
    {
        party = Campaign.Current?.CampaignObjectManager?.Find<MobileParty>(id);
        return party != null;
    }

    private static float Distance(MobileParty first, MobileParty second) =>
        first.Position.Distance(second.Position);

    private static void StageAtHold(MobileParty party, CampaignVec2 position)
    {
        party.Position = position;
        party.SetMoveModeHold();
        party.SetNavigationModeHold();
        PublishForcedPosition(party);
    }

    private static bool RestoreParty(
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        MobileParty party,
        PartyBehaviorUpdateData state)
    {
        if (!behaviorSnapshot.TryApply(party, state, out _))
            return false;

        party.Position = state.PartyPosition;
        PublishForcedPosition(party);
        return true;
    }

    private static bool TryVerifyRestored(
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        MobileParty party,
        PartyBehaviorUpdateData expected)
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
            actual.IsCurrentlyAtSea == expected.IsCurrentlyAtSea;
    }

    private static void PublishForcedPosition(MobileParty party) =>
        MessageBroker.Instance.Publish(
            typeof(FollowPartyFixtureCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea));

    private sealed class FixtureState
    {
        public MobileParty PlayerParty { get; }
        public MobileParty TargetParty { get; }
        public PartyBehaviorUpdateData PlayerState { get; }
        public PartyBehaviorUpdateData TargetState { get; }

        public FixtureState(
            MobileParty playerParty,
            MobileParty targetParty,
            PartyBehaviorUpdateData playerState,
            PartyBehaviorUpdateData targetState)
        {
            PlayerParty = playerParty;
            TargetParty = targetParty;
            PlayerState = playerState;
            TargetState = targetState;
        }
    }
}
