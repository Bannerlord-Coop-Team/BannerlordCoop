using Common;
using Common.Commands;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;

namespace GameInterface.Services.Villages.Commands;

public class RaidDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class AllowRaidAiInterventionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mapevent";

        public string Name => "allow_raid_ai_intervention";

        public string Description => "Controls raid ai intervention for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("mode", "The mode."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var value = args[0].ToLowerInvariant();
            switch (value)
            {
                case "on":
                case "true":
                case "1":
                    return Succeeded(ApplyRaidAiInterventionConfig(true));
                case "off":
                case "false":
                case "0":
                    return Succeeded(ApplyRaidAiInterventionConfig(false));
                case "toggle":
                    return Succeeded(ApplyRaidAiInterventionConfig(!MapEventConfig.AllowRaidAiIntervention));
                case "status":
                    return Succeeded(RaidAiInterventionConfigHandler.StatusText);
                default:
                    return Failed("Invalid action. Use on, off, toggle, or status.");
            }
        }
    }

    private static string ApplyRaidAiInterventionConfig(bool allow)
    {
        MapEventConfig.AllowRaidAiIntervention = allow;

        if (ModInformation.IsServer)
        {
            if (ContainerProvider.TryResolve<RaidAiInterventionConfigHandler>(out var handler))
                handler.SetAndBroadcast(allow);

            return RaidAiInterventionConfigHandler.StatusText;
        }

        if (ContainerProvider.TryResolve<INetwork>(out var network))
            network.SendAll(new NetworkRequestRaidAiInterventionConfigChange(allow));

        return RaidAiInterventionConfigHandler.StatusText + " (server update requested)";
    }

#if DEBUG
    public sealed class PrepareRaidLootWarningCoopCommand : ICoopCommand
    {
        private readonly IRaidLootWarningFixture fixture;

        public PrepareRaidLootWarningCoopCommand(IRaidLootWarningFixture fixture)
        {
            this.fixture = fixture;
        }

        public string Prefix => "coop.debug.mapevent";
        public string Name => "raid_loot_warning_prepare";
        public string Description => "Stages Polisia on an explicitly disposable baseline for issue 3262.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "Registered controller id, or only-connected for a one-client session."),
            new ExpectedArgs("baseline", "Must be disposable-baseline; reload the copied baseline between cases."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Prepare(args[0], args[1]);
    }

    public sealed class RaidLootWarningStateCoopCommand : ICoopCommand
    {
        private readonly IRaidLootWarningFixture fixture;

        public RaidLootWarningStateCoopCommand(IRaidLootWarningFixture fixture)
        {
            this.fixture = fixture;
        }

        public string Prefix => "coop.debug.mapevent";
        public string Name => "raid_loot_warning_state";
        public string Description => "Reads issue 3262 party, event and native loot UI state without advancing it.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "Registered controller id, or only-connected for a one-client session."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.ReadState(args[0]);
    }

    public sealed class StartRaidLootWarningCoopCommand : ICoopCommand
    {
        private readonly IRaidLootWarningFixture fixture;

        public StartRaidLootWarningCoopCommand(IRaidLootWarningFixture fixture)
        {
            this.fixture = fixture;
        }

        public string Prefix => "coop.debug.mapevent";
        public string Name => "raid_loot_warning_start";
        public string Description => "Requests the production settlement entry for issue 3262.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "Registered controller id, or only-connected for a one-client session."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.StartRaid(args[0]);
    }

    public sealed class RequestRaidLootWarningCoopCommand : ICoopCommand
    {
        private readonly IRaidLootWarningFixture fixture;

        public RequestRaidLootWarningCoopCommand(IRaidLootWarningFixture fixture)
        {
            this.fixture = fixture;
        }

        public string Prefix => "coop.debug.mapevent";
        public string Name => "raid_loot_warning_request_raid";
        public string Description => "Requests the issue 3262 production village raid after settlement entry is approved.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "Registered controller id, or only-connected for a one-client session."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.RequestRaid(args[0]);
    }

    public sealed class CompleteRaidLootWarningSimulationCoopCommand : ICoopCommand
    {
        private readonly IRaidLootWarningFixture fixture;

        public CompleteRaidLootWarningSimulationCoopCommand(IRaidLootWarningFixture fixture)
        {
            this.fixture = fixture;
        }

        public string Prefix => "coop.debug.mapevent";
        public string Name => "raid_loot_warning_complete_simulation";
        public string Description => "Resolves the captured simulation on the server or accepts its completed result on the client.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "Registered controller id, or only-connected for a one-client session."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.CompleteSimulation(args[0]);
    }

    public sealed class CompleteRaidLootWarningPartyCoopCommand : ICoopCommand
    {
        private readonly IRaidLootWarningFixture fixture;

        public CompleteRaidLootWarningPartyCoopCommand(IRaidLootWarningFixture fixture)
        {
            this.fixture = fixture;
        }

        public string Prefix => "coop.debug.mapevent";
        public string Name => "raid_loot_warning_complete_party";
        public string Description => "Completes the issue 3262 production loot Party screen action.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "Registered controller id, or only-connected for a one-client session."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.CompleteLootParty(args[0]);
    }

    public sealed class ShowRaidLootWarningCoopCommand : ICoopCommand
    {
        private readonly IRaidLootWarningFixture fixture;

        public ShowRaidLootWarningCoopCommand(IRaidLootWarningFixture fixture)
        {
            this.fixture = fixture;
        }

        public string Prefix => "coop.debug.mapevent";
        public string Name => "raid_loot_warning_show";
        public string Description => "Runs the issue 3262 production inventory completion action.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "Registered controller id, or only-connected for a one-client session."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.ShowLootWarning(args[0]);
    }

    public sealed class AcceptRaidLootWarningCoopCommand : ICoopCommand
    {
        private readonly IRaidLootWarningFixture fixture;

        public AcceptRaidLootWarningCoopCommand(IRaidLootWarningFixture fixture)
        {
            this.fixture = fixture;
        }

        public string Prefix => "coop.debug.mapevent";
        public string Name => "raid_loot_warning_accept";
        public string Description => "Runs the captured issue 3262 warning affirmative action.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "Registered controller id, or only-connected for a one-client session."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.AcceptLootWarning(args[0]);
    }
#endif
}
