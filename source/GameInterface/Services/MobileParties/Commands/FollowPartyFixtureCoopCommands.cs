using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.MobileParties.Commands;

public interface ISetupCoopCommand : ICoopCommand
{
}

public sealed class SetupCoopCommand : LegacyCoopCommand, ISetupCoopCommand
{
    public SetupCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "follow_fixture_setup",
            "Runs fixture setup for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("playerPartyId", "The player party id."),
            },
            FollowPartyFixtureCommands.Setup)
    {
    }
}

public interface IFollowCoopCommand : ICoopCommand
{
}

public sealed class FollowCoopCommand : LegacyCoopCommand, IFollowCoopCommand
{
    public FollowCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "follow_fixture_follow",
            "Runs fixture follow for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("targetPartyId", "The target party id."),
            },
            FollowPartyFixtureCommands.Follow)
    {
    }
}

public interface IMoveTargetCoopCommand : ICoopCommand
{
}

public sealed class MoveTargetCoopCommand : LegacyCoopCommand, IMoveTargetCoopCommand
{
    public MoveTargetCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "follow_fixture_move_target",
            "Runs fixture move target for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            FollowPartyFixtureCommands.MoveTarget)
    {
    }
}

public interface IStateCoopCommand : ICoopCommand
{
}

public sealed class StateCoopCommand : LegacyCoopCommand, IStateCoopCommand
{
    public StateCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "follow_fixture_state",
            "Runs fixture state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("playerPartyId", "The player party id."),
                new ExpectedArgs("targetPartyId", "The target party id."),
            },
            FollowPartyFixtureCommands.State)
    {
    }
}

public interface IRestoreCoopCommand : ICoopCommand
{
}

public sealed class RestoreCoopCommand : LegacyCoopCommand, IRestoreCoopCommand
{
    public RestoreCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "follow_fixture_restore",
            "Runs fixture restore for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            FollowPartyFixtureCommands.Restore)
    {
    }
}
