using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.SiegeEvents.Commands;

public interface IStartPrisonerPromptFixtureCoopCommand : ICoopCommand
{
}

public sealed class StartPrisonerPromptFixtureCoopCommand : LegacyCoopCommand, IStartPrisonerPromptFixtureCoopCommand
{
    public StartPrisonerPromptFixtureCoopCommand()
        : base(
            "coop.debug.siege",
            "prisoner_prompt_fixture_start",
            "Runs prompt fixture start for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The controller id."),
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.StartPrisonerPromptFixture)
    {
    }
}

public interface IPrisonerPromptFixtureStateCoopCommand : ICoopCommand
{
}

public sealed class PrisonerPromptFixtureStateCoopCommand : LegacyCoopCommand, IPrisonerPromptFixtureStateCoopCommand
{
    public PrisonerPromptFixtureStateCoopCommand()
        : base(
            "coop.debug.siege",
            "prisoner_prompt_fixture_state",
            "Runs prompt fixture state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The controller id."),
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.PrisonerPromptFixtureState)
    {
    }
}

public interface IRestorePrisonerPromptFixtureCoopCommand : ICoopCommand
{
}

public sealed class RestorePrisonerPromptFixtureCoopCommand : LegacyCoopCommand, IRestorePrisonerPromptFixtureCoopCommand
{
    public RestorePrisonerPromptFixtureCoopCommand()
        : base(
            "coop.debug.siege",
            "prisoner_prompt_fixture_restore",
            "Runs prompt fixture restore for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            SiegeDebugCommand.RestorePrisonerPromptFixture)
    {
    }
}

public interface IStartArmyReliefCoopCommand : ICoopCommand
{
}

public sealed class StartArmyReliefCoopCommand : LegacyCoopCommand, IStartArmyReliefCoopCommand
{
    public StartArmyReliefCoopCommand()
        : base(
            "coop.debug.siege",
            "start_army_relief",
            "Starts army relief for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The controller id."),
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("armyPartyCount", "The army party count.", isRequired: false),
            },
            SiegeDebugCommand.StartArmyRelief)
    {
    }
}

public interface IArmyReliefStateCoopCommand : ICoopCommand
{
}

public sealed class ArmyReliefStateCoopCommand : LegacyCoopCommand, IArmyReliefStateCoopCommand
{
    public ArmyReliefStateCoopCommand()
        : base(
            "coop.debug.siege",
            "army_relief_state",
            "Runs relief state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The controller id."),
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.ArmyReliefState)
    {
    }
}

public interface IRequestBesiegeCoopCommand : ICoopCommand
{
}

public sealed class RequestBesiegeCoopCommand : LegacyCoopCommand, IRequestBesiegeCoopCommand
{
    public RequestBesiegeCoopCommand()
        : base(
            "coop.debug.siege",
            "request_besiege",
            "Requests besiege for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.RequestBesiege)
    {
    }
}

public interface IRequestAssaultCoopCommand : ICoopCommand
{
}

public sealed class RequestAssaultCoopCommand : LegacyCoopCommand, IRequestAssaultCoopCommand
{
    public RequestAssaultCoopCommand()
        : base(
            "coop.debug.siege",
            "request_assault",
            "Requests assault for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.RequestAssault)
    {
    }
}

public interface IJoinActiveAssaultCoopCommand : ICoopCommand
{
}

public sealed class JoinActiveAssaultCoopCommand : LegacyCoopCommand, IJoinActiveAssaultCoopCommand
{
    public JoinActiveAssaultCoopCommand()
        : base(
            "coop.debug.siege",
            "join_active_assault",
            "Joins active assault for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.JoinActiveAssault)
    {
    }
}

public interface IAssaultEntryStateCoopCommand : ICoopCommand
{
}

public sealed class AssaultEntryStateCoopCommand : LegacyCoopCommand, IAssaultEntryStateCoopCommand
{
    public AssaultEntryStateCoopCommand()
        : base(
            "coop.debug.siege",
            "assault_entry_state",
            "Runs entry state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            SiegeDebugCommand.AssaultEntryState)
    {
    }
}

public interface ILeaveCoopCommand : ICoopCommand
{
}

public sealed class LeaveCoopCommand : LegacyCoopCommand, ILeaveCoopCommand
{
    public LeaveCoopCommand()
        : base(
            "coop.debug.siege",
            "leave",
            "Leaves the relevant state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            SiegeDebugCommand.Leave)
    {
    }
}

public interface ILeaveSettlementCoopCommand : ICoopCommand
{
}

public sealed class LeaveSettlementCoopCommand : LegacyCoopCommand, ILeaveSettlementCoopCommand
{
    public LeaveSettlementCoopCommand()
        : base(
            "coop.debug.siege",
            "leave_settlement",
            "Leaves settlement for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            SiegeDebugCommand.LeaveSettlement)
    {
    }
}

public interface IStartSiegeCoopCommand : ICoopCommand
{
}

public sealed class StartSiegeCoopCommand : LegacyCoopCommand, IStartSiegeCoopCommand
{
    public StartSiegeCoopCommand()
        : base(
            "coop.debug.siege",
            "start",
            "Starts the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("besiegerPartyId", "The besieger party id.", isRequired: false),
            },
            SiegeDebugCommand.StartSiege)
    {
    }
}

public interface IStopSiegeCoopCommand : ICoopCommand
{
}

public sealed class StopSiegeCoopCommand : LegacyCoopCommand, IStopSiegeCoopCommand
{
    public StopSiegeCoopCommand()
        : base(
            "coop.debug.siege",
            "stop",
            "Stops the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("originalX", "The original x."),
                new ExpectedArgs("originalY", "The original y."),
                new ExpectedArgs("originalIsOnLand", "The original is on land."),
            },
            SiegeDebugCommand.StopSiege)
    {
    }
}

public interface IJoinPlayersCoopCommand : ICoopCommand
{
}

public sealed class JoinPlayersCoopCommand : LegacyCoopCommand, IJoinPlayersCoopCommand
{
    public JoinPlayersCoopCommand()
        : base(
            "coop.debug.siege",
            "join_players",
            "Joins players for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("expectedPlayerCount", "The expected player count."),
            },
            SiegeDebugCommand.JoinPlayers)
    {
    }
}

public interface IPlayerStateCoopCommand : ICoopCommand
{
}

public sealed class PlayerStateCoopCommand : LegacyCoopCommand, IPlayerStateCoopCommand
{
    public PlayerStateCoopCommand()
        : base(
            "coop.debug.siege",
            "player_state",
            "Runs state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyId", "The party id."),
            },
            SiegeDebugCommand.PlayerState)
    {
    }
}

public interface IPrepareLaddersOnlyCoopCommand : ICoopCommand
{
}

public sealed class PrepareLaddersOnlyCoopCommand : LegacyCoopCommand, IPrepareLaddersOnlyCoopCommand
{
    public PrepareLaddersOnlyCoopCommand()
        : base(
            "coop.debug.siege",
            "prepare_ladders_only",
            "Prepares ladders only for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.PrepareLaddersOnly)
    {
    }
}

public interface IStageMachinesCoopCommand : ICoopCommand
{
}

public sealed class StageMachinesCoopCommand : LegacyCoopCommand, IStageMachinesCoopCommand
{
    public StageMachinesCoopCommand()
        : base(
            "coop.debug.siege",
            "stage_machines",
            "Stages machines for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.StageMachines)
    {
    }
}

public interface IStartAssaultCoopCommand : ICoopCommand
{
}

public sealed class StartAssaultCoopCommand : LegacyCoopCommand, IStartAssaultCoopCommand
{
    public StartAssaultCoopCommand()
        : base(
            "coop.debug.siege",
            "assault",
            "Runs the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.StartAssault)
    {
    }
}

public interface ITerminalStatusCoopCommand : ICoopCommand
{
}

public sealed class TerminalStatusCoopCommand : LegacyCoopCommand, ITerminalStatusCoopCommand
{
    public TerminalStatusCoopCommand()
        : base(
            "coop.debug.siege",
            "terminal_status",
            "Runs status for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.TerminalStatus)
    {
    }
}

public interface IResolveStarvationCoopCommand : ICoopCommand
{
}

public sealed class ResolveStarvationCoopCommand : LegacyCoopCommand, IResolveStarvationCoopCommand
{
    public ResolveStarvationCoopCommand()
        : base(
            "coop.debug.siege",
            "resolve_starvation",
            "Resolves starvation for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.ResolveStarvation)
    {
    }
}

public interface IListSiegesCoopCommand : ICoopCommand
{
}

public sealed class ListSiegesCoopCommand : LegacyCoopCommand, IListSiegesCoopCommand
{
    public ListSiegesCoopCommand()
        : base(
            "coop.debug.siege",
            "list",
            "Lists the relevant state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            SiegeDebugCommand.ListSieges)
    {
    }
}

public interface IGraphStateCoopCommand : ICoopCommand
{
}

public sealed class GraphStateCoopCommand : LegacyCoopCommand, IGraphStateCoopCommand
{
    public GraphStateCoopCommand()
        : base(
            "coop.debug.siege",
            "graph",
            "Runs the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.GraphState)
    {
    }
}

public interface IFocusSettlementCoopCommand : ICoopCommand
{
}

public sealed class FocusSettlementCoopCommand : LegacyCoopCommand, IFocusSettlementCoopCommand
{
    public FocusSettlementCoopCommand()
        : base(
            "coop.debug.siege",
            "focus",
            "Focuses the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SiegeDebugCommand.FocusSettlement)
    {
    }
}

public interface IDumpPartyCoopCommand : ICoopCommand
{
}

public sealed class DumpPartyCoopCommand : LegacyCoopCommand, IDumpPartyCoopCommand
{
    public DumpPartyCoopCommand()
        : base(
            "coop.debug.siege",
            "dump_party",
            "Dumps party for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroNameOrPartyId", "The exact hero name or party id; quote names containing spaces."),
            },
            SiegeDebugCommand.DumpParty)
    {
    }
}

public interface IDumpEnginesCoopCommand : ICoopCommand
{
}

public sealed class DumpEnginesCoopCommand : LegacyCoopCommand, IDumpEnginesCoopCommand
{
    public DumpEnginesCoopCommand()
        : base(
            "coop.debug.siege",
            "dump_engines",
            "Dumps engines for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            SiegeDebugCommand.DumpEngines)
    {
    }
}

public interface IDumpMachinesCoopCommand : ICoopCommand
{
}

public sealed class DumpMachinesCoopCommand : LegacyCoopCommand, IDumpMachinesCoopCommand
{
    public DumpMachinesCoopCommand()
        : base(
            "coop.debug.siege",
            "dump_machines",
            "Dumps machines for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("includeAll", "The include all.", isRequired: false),
            },
            SiegeDebugCommand.DumpMachines)
    {
    }
}
