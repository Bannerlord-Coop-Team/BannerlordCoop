using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Tournaments.Commands;

public interface IAddTournamentToTownCoopCommand : ICoopCommand
{
}

public sealed class AddTournamentToTownCoopCommand : LegacyCoopCommand, IAddTournamentToTownCoopCommand
{
    public AddTournamentToTownCoopCommand()
        : base(
            "coop.debug.tournaments",
            "add_tournament_to_town",
            "Adds tournament to town for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townNameOrId", "The exact town name or id; quote names containing spaces."),
            },
            TournamentDebugCommand.AddTournamentToTown)
    {
    }
}

#if DEBUG
public interface IBeginDanusticaFixtureCoopCommand : ICoopCommand
{
}

public sealed class BeginDanusticaFixtureCoopCommand : LegacyCoopCommand, IBeginDanusticaFixtureCoopCommand
{
    public BeginDanusticaFixtureCoopCommand()
        : base(
            "coop.debug.tournaments",
            "danustica_fixture_begin",
            "Runs fixture begin for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TournamentDebugCommand.BeginDanusticaFixture)
    {
    }
}
#endif

#if DEBUG
public interface IDanusticaFixtureStateCoopCommand : ICoopCommand
{
}

public sealed class DanusticaFixtureStateCoopCommand : LegacyCoopCommand, IDanusticaFixtureStateCoopCommand
{
    public DanusticaFixtureStateCoopCommand()
        : base(
            "coop.debug.tournaments",
            "danustica_fixture_state",
            "Runs fixture state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TournamentDebugCommand.DanusticaFixtureState)
    {
    }
}
#endif

#if DEBUG
public interface IRestoreDanusticaFixtureCoopCommand : ICoopCommand
{
}

public sealed class RestoreDanusticaFixtureCoopCommand : LegacyCoopCommand, IRestoreDanusticaFixtureCoopCommand
{
    public RestoreDanusticaFixtureCoopCommand()
        : base(
            "coop.debug.tournaments",
            "danustica_fixture_restore",
            "Runs fixture restore for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TournamentDebugCommand.RestoreDanusticaFixture)
    {
    }
}
#endif

#if DEBUG
public interface IAbortDanusticaFixtureCoopCommand : ICoopCommand
{
}

public sealed class AbortDanusticaFixtureCoopCommand : LegacyCoopCommand, IAbortDanusticaFixtureCoopCommand
{
    public AbortDanusticaFixtureCoopCommand()
        : base(
            "coop.debug.tournaments",
            "danustica_fixture_abort",
            "Runs fixture abort for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TournamentDebugCommand.AbortDanusticaFixture)
    {
    }
}
#endif

#if DEBUG
public interface IRequestDanusticaJoinCoopCommand : ICoopCommand
{
}

public sealed class RequestDanusticaJoinCoopCommand : LegacyCoopCommand, IRequestDanusticaJoinCoopCommand
{
    public RequestDanusticaJoinCoopCommand()
        : base(
            "coop.debug.tournaments",
            "danustica_request_join",
            "Runs request join for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TournamentDebugCommand.RequestDanusticaJoin)
    {
    }
}
#endif

#if DEBUG
public interface IRequestDanusticaStartCoopCommand : ICoopCommand
{
}

public sealed class RequestDanusticaStartCoopCommand : LegacyCoopCommand, IRequestDanusticaStartCoopCommand
{
    public RequestDanusticaStartCoopCommand()
        : base(
            "coop.debug.tournaments",
            "danustica_request_start",
            "Runs request start for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TournamentDebugCommand.RequestDanusticaStart)
    {
    }
}
#endif

#if DEBUG
public interface IRequestDanusticaChoiceCoopCommand : ICoopCommand
{
}

public sealed class RequestDanusticaChoiceCoopCommand : LegacyCoopCommand, IRequestDanusticaChoiceCoopCommand
{
    public RequestDanusticaChoiceCoopCommand()
        : base(
            "coop.debug.tournaments",
            "danustica_request_choice",
            "Runs request choice for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("choice", "The choice."),
            },
            TournamentDebugCommand.RequestDanusticaChoice)
    {
    }
}
#endif

#if DEBUG
public interface IRequestDanusticaLeaveCoopCommand : ICoopCommand
{
}

public sealed class RequestDanusticaLeaveCoopCommand : LegacyCoopCommand, IRequestDanusticaLeaveCoopCommand
{
    public RequestDanusticaLeaveCoopCommand()
        : base(
            "coop.debug.tournaments",
            "danustica_request_leave",
            "Runs request leave for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TournamentDebugCommand.RequestDanusticaLeave)
    {
    }
}
#endif

#if DEBUG
public interface IObserveDanusticaCommandCoopCommand : ICoopCommand
{
}

public sealed class ObserveDanusticaCommandCoopCommand : LegacyCoopCommand, IObserveDanusticaCommandCoopCommand
{
    public ObserveDanusticaCommandCoopCommand()
        : base(
            "coop.debug.tournaments",
            "danustica_observe",
            "Runs observe for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TournamentDebugCommand.ObserveDanusticaCommand)
    {
    }
}
#endif
