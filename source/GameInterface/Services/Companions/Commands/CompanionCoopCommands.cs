using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Companions.Commands;

public interface ICompanionListWanderersCommand : ICoopCommand
{
}

public sealed class CompanionListWanderersCommand : LegacyCoopCommand, ICompanionListWanderersCommand
{
    public CompanionListWanderersCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "list_wanderers",
            "Lists available wanderer heroes.",
            System.Array.Empty<IExpectedArgs>(),
            CompanionsCommands.ListWanderersCommand)
    {
    }
}

public interface ICompanionClearWanderersCommand : ICoopCommand
{
}

public sealed class CompanionClearWanderersCommand : LegacyCoopCommand, ICompanionClearWanderersCommand
{
    public CompanionClearWanderersCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "clear_wanderers",
            "Clears available wanderer heroes.",
            System.Array.Empty<IExpectedArgs>(),
            CompanionsCommands.ClearWanderersCommand)
    {
    }
}

public interface ICompanionRoleFixtureSetupCommand : ICoopCommand
{
}

public sealed class CompanionRoleFixtureSetupCommand : LegacyCoopCommand, ICompanionRoleFixtureSetupCommand
{
    public CompanionRoleFixtureSetupCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "role_fixture_setup",
            "Starts the companion-role fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The player controller id."),
            },
            CompanionsCommands.RoleFixtureSetupCommand)
    {
    }
}

public interface ICompanionRoleFixtureOpenConversationCommand : ICoopCommand
{
}

public sealed class CompanionRoleFixtureOpenConversationCommand : LegacyCoopCommand, ICompanionRoleFixtureOpenConversationCommand
{
    public CompanionRoleFixtureOpenConversationCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "role_fixture_open_conversation",
            "Opens the companion-role fixture conversation.",
            System.Array.Empty<IExpectedArgs>(),
            CompanionsCommands.RoleFixtureOpenConversationCommand)
    {
    }
}

public interface ICompanionRoleFixtureConversationStateCommand : ICoopCommand
{
}

public sealed class CompanionRoleFixtureConversationStateCommand : LegacyCoopCommand, ICompanionRoleFixtureConversationStateCommand
{
    public CompanionRoleFixtureConversationStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "role_fixture_conversation_state",
            "Reports companion-role conversation state.",
            System.Array.Empty<IExpectedArgs>(),
            CompanionsCommands.RoleFixtureConversationStateCommand)
    {
    }
}

public interface ICompanionRoleFixturePrepareClientCommand : ICoopCommand
{
}

public sealed class CompanionRoleFixturePrepareClientCommand : LegacyCoopCommand, ICompanionRoleFixturePrepareClientCommand
{
    public CompanionRoleFixturePrepareClientCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "role_fixture_prepare_client",
            "Prepares the client for the companion-role fixture.",
            System.Array.Empty<IExpectedArgs>(),
            CompanionsCommands.RoleFixturePrepareClientCommand)
    {
    }
}

public interface ICompanionRoleFixtureAssignScoutCommand : ICoopCommand
{
}

public sealed class CompanionRoleFixtureAssignScoutCommand : LegacyCoopCommand, ICompanionRoleFixtureAssignScoutCommand
{
    public CompanionRoleFixtureAssignScoutCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "role_fixture_assign_scout",
            "Assigns the fixture companion as scout.",
            System.Array.Empty<IExpectedArgs>(),
            CompanionsCommands.RoleFixtureAssignScoutCommand)
    {
    }
}

public interface ICompanionRoleFixtureStateCommand : ICoopCommand
{
}

public sealed class CompanionRoleFixtureStateCommand : LegacyCoopCommand, ICompanionRoleFixtureStateCommand
{
    public CompanionRoleFixtureStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "role_fixture_state",
            "Reports companion-role fixture state.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyId", "The registered mobile party id."),
            },
            CompanionsCommands.RoleFixtureStateCommand)
    {
    }
}

public interface ICompanionScoutRoleStateCommand : ICoopCommand
{
}

public sealed class CompanionScoutRoleStateCommand : LegacyCoopCommand, ICompanionScoutRoleStateCommand
{
    public CompanionScoutRoleStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "scout_role_state",
            "Reports the scout assigned to a party.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyId", "The registered mobile party id."),
            },
            CompanionsCommands.ScoutRoleStateCommand)
    {
    }
}

public interface ICompanionRoleFixtureRestoreCommand : ICoopCommand
{
}

public sealed class CompanionRoleFixtureRestoreCommand : LegacyCoopCommand, ICompanionRoleFixtureRestoreCommand
{
    public CompanionRoleFixtureRestoreCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "role_fixture_restore",
            "Restores the companion-role fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The player controller id."),
            },
            CompanionsCommands.RoleFixtureRestoreCommand)
    {
    }
}

public interface ICompanionDismissalFixtureSetupCommand : ICoopCommand
{
}

public sealed class CompanionDismissalFixtureSetupCommand : LegacyCoopCommand, ICompanionDismissalFixtureSetupCommand
{
    public CompanionDismissalFixtureSetupCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "dismissal_fixture_setup",
            "Starts the companion-dismissal fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The player controller id."),
            },
            CompanionsCommands.DismissalFixtureSetupCommand)
    {
    }
}

public interface ICompanionDismissalFixturePrepareDismissCommand : ICoopCommand
{
}

public sealed class CompanionDismissalFixturePrepareDismissCommand : LegacyCoopCommand, ICompanionDismissalFixturePrepareDismissCommand
{
    public CompanionDismissalFixturePrepareDismissCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "dismissal_fixture_prepare_dismiss",
            "Prepares the fixture companion dismissal.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The player controller id."),
                new ExpectedArgs("initialCopies", "The positive initial roster count."),
            },
            CompanionsCommands.DismissalFixturePrepareDismissCommand)
    {
    }
}

public interface ICompanionDismissalFixtureTriggerConsequenceCommand : ICoopCommand
{
}

public sealed class CompanionDismissalFixtureTriggerConsequenceCommand : LegacyCoopCommand, ICompanionDismissalFixtureTriggerConsequenceCommand
{
    public CompanionDismissalFixtureTriggerConsequenceCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "dismissal_fixture_trigger_consequence",
            "Triggers the companion dismissal consequence.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("dismissedHeroId", "The registered dismissed hero id."),
            },
            CompanionsCommands.DismissalFixtureTriggerConsequenceCommand)
    {
    }
}

public interface ICompanionDismissalFixtureCompletionCommand : ICoopCommand
{
}

public sealed class CompanionDismissalFixtureCompletionCommand : LegacyCoopCommand, ICompanionDismissalFixtureCompletionCommand
{
    public CompanionDismissalFixtureCompletionCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "dismissal_fixture_completion",
            "Reports companion dismissal completion.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("dismissedHeroId", "The registered dismissed hero id."),
            },
            CompanionsCommands.DismissalFixtureCompletionCommand)
    {
    }
}

public interface ICompanionDismissalFixtureReleaseEncounterCommand : ICoopCommand
{
}

public sealed class CompanionDismissalFixtureReleaseEncounterCommand : LegacyCoopCommand, ICompanionDismissalFixtureReleaseEncounterCommand
{
    public CompanionDismissalFixtureReleaseEncounterCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "dismissal_fixture_release_encounter",
            "Releases the dismissal fixture encounter.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("dismissedHeroId", "The registered dismissed hero id."),
            },
            CompanionsCommands.DismissalFixtureReleaseEncounterCommand)
    {
    }
}

public interface ICompanionDismissalFixtureRequestReplacementCommand : ICoopCommand
{
}

public sealed class CompanionDismissalFixtureRequestReplacementCommand : LegacyCoopCommand, ICompanionDismissalFixtureRequestReplacementCommand
{
    public CompanionDismissalFixtureRequestReplacementCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "dismissal_fixture_request_replacement",
            "Requests the dismissal fixture replacement.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("replacementHeroId", "The registered replacement hero id."),
            },
            CompanionsCommands.DismissalFixtureRequestReplacementCommand)
    {
    }
}

public interface ICompanionDismissalFixtureStateCommand : ICoopCommand
{
}

public sealed class CompanionDismissalFixtureStateCommand : LegacyCoopCommand, ICompanionDismissalFixtureStateCommand
{
    public CompanionDismissalFixtureStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "dismissal_fixture_state",
            "Reports companion dismissal fixture state.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyId", "The registered mobile party id."),
                new ExpectedArgs("dismissedHeroId", "The registered dismissed hero id."),
                new ExpectedArgs("replacementHeroId", "The registered replacement hero id."),
            },
            CompanionsCommands.DismissalFixtureStateCommand)
    {
    }
}

public interface ICompanionDismissalFixtureRestoreCommand : ICoopCommand
{
}

public sealed class CompanionDismissalFixtureRestoreCommand : LegacyCoopCommand, ICompanionDismissalFixtureRestoreCommand
{
    public CompanionDismissalFixtureRestoreCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "dismissal_fixture_restore",
            "Restores the companion-dismissal fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The player controller id."),
            },
            CompanionsCommands.DismissalFixtureRestoreCommand)
    {
    }
}

public interface ICompanionOpenPartyScreenCommand : ICoopCommand
{
}

public sealed class CompanionOpenPartyScreenCommand : LegacyCoopCommand, ICompanionOpenPartyScreenCommand
{
    public CompanionOpenPartyScreenCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "open_party_screen",
            "Opens the companion party screen.",
            System.Array.Empty<IExpectedArgs>(),
            CompanionsCommands.OpenPartyScreenCommand)
    {
    }
}

public interface ICompanionClosePartyScreenCommand : ICoopCommand
{
}

public sealed class CompanionClosePartyScreenCommand : LegacyCoopCommand, ICompanionClosePartyScreenCommand
{
    public CompanionClosePartyScreenCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "close_party_screen",
            "Closes the companion party screen.",
            System.Array.Empty<IExpectedArgs>(),
            CompanionsCommands.ClosePartyScreenCommand)
    {
    }
}

public interface ICompanionCommitPartyScreenCommand : ICoopCommand
{
}

public sealed class CompanionCommitPartyScreenCommand : LegacyCoopCommand, ICompanionCommitPartyScreenCommand
{
    public CompanionCommitPartyScreenCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.companions",
            "commit_party_screen",
            "Commits changes on the companion party screen.",
            System.Array.Empty<IExpectedArgs>(),
            CompanionsCommands.CommitPartyScreenCommand)
    {
    }
}
