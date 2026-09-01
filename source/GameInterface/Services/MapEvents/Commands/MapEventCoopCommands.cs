using Common.Commands;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.MapEvents.Commands;

public interface IMapEventLegacyCommandResult
{
    CoopCommandResult FromOutput(string output);

    CoopCommandResult FromOutput(string output, string successfulPrefix);
}

public sealed class MapEventLegacyCommandResult : IMapEventLegacyCommandResult
{
    public CoopCommandResult FromOutput(string output)
    {
        return FromOutput(output, null);
    }

    public CoopCommandResult FromOutput(string output, string successfulPrefix)
    {
        if (output == null) return new CoopCommandResult(false, "Command returned no output.", "command_failed");

        bool isKnownSuccess = output.StartsWith("Map event id:", StringComparison.Ordinal) ||
                              successfulPrefix != null && output.StartsWith(successfulPrefix, StringComparison.Ordinal);
        bool succeeded = isKnownSuccess || !LooksLikeFailure(output);
        return new CoopCommandResult(succeeded, output, succeeded ? null : "command_failed");
    }

    private static bool LooksLikeFailure(string output)
    {
        string[] failurePrefixes =
        {
            "Usage:",
            "Failed",
            "Unable",
            "No ",
            "Run this",
            "Command can",
            "The host has disabled",
            "Cannot ",
            "Could not",
            "Refusing",
            "Prepare ",
            "Both ",
            "Player parties must",
            "Attacker and defender",
            "Exists must",
            "State requires",
            "A ",
            "The ",
            "Party ",
            "Character ",
            "Settlement ",
            "Object manager",
            "Network ",
            "Battle agent",
            "Active mount",
            "Upgrade ",
            "Clan-party",
            "Map event",
            "Invalid ",
            "Player parties",
            "Player party",
            "Player '",
        };

        foreach (string prefix in failurePrefixes)
        {
            if (output.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return HasFailurePhrase(output);
    }

    private static bool HasFailurePhrase(string output)
    {
        return output.IndexOf(" not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" is not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" are not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" did not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" does not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" has no ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" has not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" is unavailable", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" is already ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" cannot ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" must ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" already active", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.StartsWith("Add the ", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Begin the ", StringComparison.OrdinalIgnoreCase);
    }
}

public interface IClickDeploymentReadyCommand : ICoopCommand
{
}

public sealed class ClickDeploymentReadyCommand : IClickDeploymentReadyCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public ClickDeploymentReadyCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "click_deployment_ready";

    public string Description => "Runs the click deployment ready debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.ClickDeploymentReady(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IDeploymentStateCommand : ICoopCommand
{
}

public sealed class DeploymentStateCommand : IDeploymentStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public DeploymentStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "deployment_state";

    public string Description => "Reports deployment state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.DeploymentState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IFinishDeploymentCommand : ICoopCommand
{
}

public sealed class FinishDeploymentCommand : IFinishDeploymentCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public FinishDeploymentCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "finish_deployment";

    public string Description => "Runs the finish deployment debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.FinishDeployment(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IToggleScoreboardCommand : ICoopCommand
{
}

public sealed class ToggleScoreboardCommand : IToggleScoreboardCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public ToggleScoreboardCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "toggle_scoreboard";

    public string Description => "Runs the toggle scoreboard debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.ToggleScoreboard(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ICollapseScoreboardPartiesCommand : ICoopCommand
{
}

public sealed class CollapseScoreboardPartiesCommand : ICollapseScoreboardPartiesCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public CollapseScoreboardPartiesCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "collapse_scoreboard_parties";

    public string Description => "Runs the collapse scoreboard parties debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.CollapseScoreboardParties(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IScoreboardStateCommand : ICoopCommand
{
}

public sealed class ScoreboardStateCommand : IScoreboardStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public ScoreboardStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "scoreboard_state";

    public string Description => "Reports scoreboard state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.ScoreboardState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILeaveBattleCommand : ICoopCommand
{
}

public sealed class LeaveBattleCommand : ILeaveBattleCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LeaveBattleCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "leave_battle";

    public string Description => "Runs the leave battle debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.LeaveBattle(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IKillEnemyCommand : ICoopCommand
{
}

public sealed class KillEnemyCommand : IKillEnemyCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public KillEnemyCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "kill_enemy";

    public string Description => "Runs the kill enemy debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.KillOneEnemy(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IKillEnemyTeamCommand : ICoopCommand
{
}

public sealed class KillEnemyTeamCommand : IKillEnemyTeamCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public KillEnemyTeamCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "kill_enemy_team";

    public string Description => "Runs the kill enemy team debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.KillEnemyTeam(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IKillOwnTeamCommand : ICoopCommand
{
}

public sealed class KillOwnTeamCommand : IKillOwnTeamCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public KillOwnTeamCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "kill_own_team";

    public string Description => "Runs the kill own team debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.BattleTeamKillCommands.KillOwnTeam(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IKmsCommand : ICoopCommand
{
}

public sealed class KmsCommand : IKmsCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public KmsCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "kms";

    public string Description => "Runs the kms debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.KillPlayerAgentCommands.KillPlayerAgent(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IPrisonerPromptStateCommand : ICoopCommand
{
}

public sealed class PrisonerPromptStateCommand : IPrisonerPromptStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PrisonerPromptStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "prisoner_prompt_state";

    public string Description => "Reports prisoner prompt state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.PrisonerPromptState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IPrisonerPromptCommand : ICoopCommand
{
}

public sealed class PrisonerPromptCommand : IPrisonerPromptCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PrisonerPromptCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "prisoner_prompt";

    public string Description => "Runs the prisoner prompt debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "The action.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.PrisonerPrompt(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IStartPlayerFieldBattleCommand : ICoopCommand
{
}

public sealed class StartPlayerFieldBattleCommand : IStartPlayerFieldBattleCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public StartPlayerFieldBattleCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "start_player_field_battle";

    public string Description => "Runs the start player field battle debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("attacker_mobile_party_id", "The attacker mobile party id.", true),
        new ExpectedArgs("defender_mobile_party_id", "The defender mobile party id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartPlayerFieldBattle(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IRestorePlayerFieldBattleCommand : ICoopCommand
{
}

public sealed class RestorePlayerFieldBattleCommand : IRestorePlayerFieldBattleCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public RestorePlayerFieldBattleCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "restore_player_field_battle";

    public string Description => "Restores or clears restore player field battle.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.RestorePlayerFieldBattle(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IRequestPlayerFieldBattleCommand : ICoopCommand
{
}

public sealed class RequestPlayerFieldBattleCommand : IRequestPlayerFieldBattleCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public RequestPlayerFieldBattleCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "request_player_field_battle";

    public string Description => "Runs the request player field battle debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("defender_mobile_party_id", "The defender mobile party id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.RequestPlayerFieldBattle(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IPlayerInteractionStateCommand : ICoopCommand
{
}

public sealed class PlayerInteractionStateCommand : IPlayerInteractionStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PlayerInteractionStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "player_interaction_state";

    public string Description => "Reports player interaction state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.PlayerInteractionState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ISubmitPlayerInteractionCommand : ICoopCommand
{
}

public sealed class SubmitPlayerInteractionCommand : ISubmitPlayerInteractionCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public SubmitPlayerInteractionCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "submit_player_interaction";

    public string Description => "Runs the submit player interaction debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("option", "The option.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.SubmitPlayerInteraction(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IStartAttackMissionCommand : ICoopCommand
{
}

public sealed class StartAttackMissionCommand : IStartAttackMissionCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public StartAttackMissionCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "start_attack_mission";

    public string Description => "Runs the start attack mission debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartAttackMission(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IStartLooterCommand : ICoopCommand
{
}

public sealed class StartLooterCommand : IStartLooterCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public StartLooterCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "start_looter";

    public string Description => "Runs the start looter debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartRandomLooterMapEvent(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IStartNearestLooterCommand : ICoopCommand
{
}

public sealed class StartNearestLooterCommand : IStartNearestLooterCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public StartNearestLooterCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "start_nearest_looter";

    public string Description => "Runs the start nearest looter debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartNearestLooterMapEvent(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IStartNearestBanditAttackCommand : ICoopCommand
{
}

public sealed class StartNearestBanditAttackCommand : IStartNearestBanditAttackCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public StartNearestBanditAttackCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "start_nearest_bandit_attack";

    public string Description => "Runs the start nearest bandit attack debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
        new ExpectedArgs("excluded_party_id", "The excluded party id.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartNearestBanditAttack(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBanditAttackFixturePrepareCommand : ICoopCommand
{
}

public sealed class BanditAttackFixturePrepareCommand : IBanditAttackFixturePrepareCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BanditAttackFixturePrepareCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "bandit_attack_fixture_prepare";

    public string Description => "Runs the bandit attack fixture prepare debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
        new ExpectedArgs("bandit_party_id", "The bandit party id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.PrepareBanditAttackFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBanditAttackFixtureStartCommand : ICoopCommand
{
}

public sealed class BanditAttackFixtureStartCommand : IBanditAttackFixtureStartCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BanditAttackFixtureStartCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "bandit_attack_fixture_start";

    public string Description => "Runs the bandit attack fixture start debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
        new ExpectedArgs("bandit_party_id", "The bandit party id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartBanditAttackFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBanditAttackFixtureStateCommand : ICoopCommand
{
}

public sealed class BanditAttackFixtureStateCommand : IBanditAttackFixtureStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BanditAttackFixtureStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "bandit_attack_fixture_state";

    public string Description => "Reports bandit attack fixture state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
        new ExpectedArgs("bandit_party_id", "The bandit party id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.GetBanditAttackFixtureState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBanditAttackFixtureRestoreCommand : ICoopCommand
{
}

public sealed class BanditAttackFixtureRestoreCommand : IBanditAttackFixtureRestoreCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BanditAttackFixtureRestoreCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "bandit_attack_fixture_restore";

    public string Description => "Restores or clears bandit attack fixture restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.RestoreBanditAttackFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IFinishNonBattleEncounterCommand : ICoopCommand
{
}

public sealed class FinishNonBattleEncounterCommand : IFinishNonBattleEncounterCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public FinishNonBattleEncounterCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "finish_non_battle_encounter";

    public string Description => "Runs the finish non battle encounter debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.FinishNonBattleEncounter(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IJoinExistingCommand : ICoopCommand
{
}

public sealed class JoinExistingCommand : IJoinExistingCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public JoinExistingCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "join_existing";

    public string Description => "Runs the join existing debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("map_event_id", "The map event id.", true),
        new ExpectedArgs("side", "The battle side.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.JoinExistingBattle(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBattleRewardFixturePrepareCommand : ICoopCommand
{
}

public sealed class BattleRewardFixturePrepareCommand : IBattleRewardFixturePrepareCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BattleRewardFixturePrepareCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "battle_reward_fixture_prepare";

    public string Description => "Runs the battle reward fixture prepare debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("initiator_controller_id", "The initiator controller id.", true),
        new ExpectedArgs("late_joiner_controller_id", "The late joiner controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.PrepareBattleRewardFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBattleRewardFixtureStartCommand : ICoopCommand
{
}

public sealed class BattleRewardFixtureStartCommand : IBattleRewardFixtureStartCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BattleRewardFixtureStartCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "battle_reward_fixture_start";

    public string Description => "Runs the battle reward fixture start debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("initiator_controller_id", "The initiator controller id.", true),
        new ExpectedArgs("late_joiner_controller_id", "The late joiner controller id.", true),
        new ExpectedArgs("army", "The army.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartBattleRewardFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBattleRewardFixtureReinforceCommand : ICoopCommand
{
}

public sealed class BattleRewardFixtureReinforceCommand : IBattleRewardFixtureReinforceCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BattleRewardFixtureReinforceCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "battle_reward_fixture_reinforce";

    public string Description => "Runs the battle reward fixture reinforce debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.ReinforceBattleRewardFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBattleRewardFixtureJoinCommand : ICoopCommand
{
}

public sealed class BattleRewardFixtureJoinCommand : IBattleRewardFixtureJoinCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BattleRewardFixtureJoinCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "battle_reward_fixture_join";

    public string Description => "Runs the battle reward fixture join debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.JoinBattleRewardFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBattleRewardFixtureBeginRoutCommand : ICoopCommand
{
}

public sealed class BattleRewardFixtureBeginRoutCommand : IBattleRewardFixtureBeginRoutCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BattleRewardFixtureBeginRoutCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "battle_reward_fixture_begin_rout";

    public string Description => "Runs the battle reward fixture begin rout debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.BeginBattleRewardFixtureRout(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBattleRewardFixtureRouteEnemiesCommand : ICoopCommand
{
}

public sealed class BattleRewardFixtureRouteEnemiesCommand : IBattleRewardFixtureRouteEnemiesCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BattleRewardFixtureRouteEnemiesCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "battle_reward_fixture_route_enemies";

    public string Description => "Runs the battle reward fixture route enemies debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.RouteBattleRewardFixtureEnemies(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBattleRewardFixtureStateCommand : ICoopCommand
{
}

public sealed class BattleRewardFixtureStateCommand : IBattleRewardFixtureStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BattleRewardFixtureStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "battle_reward_fixture_state";

    public string Description => "Reports battle reward fixture state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.GetBattleRewardFixtureState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBattleRewardClientStateCommand : ICoopCommand
{
}

public sealed class BattleRewardClientStateCommand : IBattleRewardClientStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BattleRewardClientStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "battle_reward_client_state";

    public string Description => "Reports battle reward client state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.GetBattleRewardClientState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IBattleRewardFixtureRestoreCommand : ICoopCommand
{
}

public sealed class BattleRewardFixtureRestoreCommand : IBattleRewardFixtureRestoreCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public BattleRewardFixtureRestoreCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "battle_reward_fixture_restore";

    public string Description => "Restores or clears battle reward fixture restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.RestoreBattleRewardFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IWoundedAlliedFixtureStartCommand : ICoopCommand
{
}

public sealed class WoundedAlliedFixtureStartCommand : IWoundedAlliedFixtureStartCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public WoundedAlliedFixtureStartCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "wounded_allied_fixture_start";

    public string Description => "Runs the wounded allied fixture start debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartWoundedAlliedFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IWoundedAlliedFixtureStateCommand : ICoopCommand
{
}

public sealed class WoundedAlliedFixtureStateCommand : IWoundedAlliedFixtureStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public WoundedAlliedFixtureStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "wounded_allied_fixture_state";

    public string Description => "Reports wounded allied fixture state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.GetWoundedAlliedFixtureState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IWoundedAlliedFixtureRestoreCommand : ICoopCommand
{
}

public sealed class WoundedAlliedFixtureRestoreCommand : IWoundedAlliedFixtureRestoreCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public WoundedAlliedFixtureRestoreCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "wounded_allied_fixture_restore";

    public string Description => "Restores or clears wounded allied fixture restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.RestoreWoundedAlliedFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILeaveSettlementCommand : ICoopCommand
{
}

public sealed class LeaveSettlementCommand : ILeaveSettlementCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LeaveSettlementCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "leave_settlement";

    public string Description => "Runs the leave settlement debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.LeaveSettlement(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IFinishCurrentEncounterCommand : ICoopCommand
{
}

public sealed class FinishCurrentEncounterCommand : IFinishCurrentEncounterCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public FinishCurrentEncounterCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "finish_current_encounter";

    public string Description => "Runs the finish current encounter debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.FinishCurrentEncounter(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IEnterCurrentBattleCommand : ICoopCommand
{
}

public sealed class EnterCurrentBattleCommand : IEnterCurrentBattleCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public EnterCurrentBattleCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "enter_current_battle";

    public string Description => "Runs the enter current battle debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.EnterCurrentBattle(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IFinishPlayerEncounterCommand : ICoopCommand
{
}

public sealed class FinishPlayerEncounterCommand : IFinishPlayerEncounterCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public FinishPlayerEncounterCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "finish_player_encounter";

    public string Description => "Runs the finish player encounter debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.FinishPlayerEncounter(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IConversationHoldStateCommand : ICoopCommand
{
}

public sealed class ConversationHoldStateCommand : IConversationHoldStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public ConversationHoldStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "conversation_hold_state";

    public string Description => "Reports conversation hold state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_base_id", "The party base id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.ConversationHoldState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILateJoinModeFixtureCommand : ICoopCommand
{
}

public sealed class LateJoinModeFixtureCommand : ILateJoinModeFixtureCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LateJoinModeFixtureCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "late_join_mode_fixture";

    public string Description => "Runs the late join mode fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("first_controller_id", "The first controller id.", true),
        new ExpectedArgs("joining_controller_id", "The joining controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartLateJoinModeFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILateJoinModeJoinCommand : ICoopCommand
{
}

public sealed class LateJoinModeJoinCommand : ILateJoinModeJoinCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LateJoinModeJoinCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "late_join_mode_join";

    public string Description => "Runs the late join mode join debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.JoinLateJoinModeFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILateJoinModeEnterCommand : ICoopCommand
{
}

public sealed class LateJoinModeEnterCommand : ILateJoinModeEnterCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LateJoinModeEnterCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "late_join_mode_enter";

    public string Description => "Runs the late join mode enter debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.EnterLateJoinModeFixtureMission(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

#if DEBUG
public interface ILateJoinModeBeginFieldBattleCommand : ICoopCommand
{
}

public sealed class LateJoinModeBeginFieldBattleCommand : ILateJoinModeBeginFieldBattleCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LateJoinModeBeginFieldBattleCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "late_join_mode_begin_field_battle";

    public string Description => "Runs the late join mode begin field battle debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.BeginLateJoinModeFixtureFieldBattle(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILateJoinModeDisableDyingCommand : ICoopCommand
{
}

public sealed class LateJoinModeDisableDyingCommand : ILateJoinModeDisableDyingCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LateJoinModeDisableDyingCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "late_join_mode_disable_dying";

    public string Description => "Runs the late join mode disable dying debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.DisableLateJoinModeFixtureDying(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILateJoinModeExitMissionsCommand : ICoopCommand
{
}

public sealed class LateJoinModeExitMissionsCommand : ILateJoinModeExitMissionsCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LateJoinModeExitMissionsCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "late_join_mode_exit_missions";

    public string Description => "Runs the late join mode exit missions debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.ExitLateJoinModeFixtureMissions(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILateJoinModeRestoreCommand : ICoopCommand
{
}

public sealed class LateJoinModeRestoreCommand : ILateJoinModeRestoreCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LateJoinModeRestoreCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "late_join_mode_restore";

    public string Description => "Restores or clears late join mode restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.RestoreLateJoinModeFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

#endif

public interface ILateJoinModeStateCommand : ICoopCommand
{
}

public sealed class LateJoinModeStateCommand : ILateJoinModeStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LateJoinModeStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "late_join_mode_state";

    public string Description => "Reports late join mode state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.GetLateJoinModeState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILateJoinModeCleanupCommand : ICoopCommand
{
}

public sealed class LateJoinModeCleanupCommand : ILateJoinModeCleanupCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public LateJoinModeCleanupCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "late_join_mode_cleanup";

    public string Description => "Runs the late join mode cleanup debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.CleanupLateJoinModeFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IPeacePursuitFixtureCommand : ICoopCommand
{
}

public sealed class PeacePursuitFixtureCommand : IPeacePursuitFixtureCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PeacePursuitFixtureCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "peace_pursuit_fixture";

    public string Description => "Runs the peace pursuit fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.GetPeacePursuitFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IPeacePursuitStateCommand : ICoopCommand
{
}

public sealed class PeacePursuitStateCommand : IPeacePursuitStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PeacePursuitStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "peace_pursuit_state";

    public string Description => "Reports peace pursuit state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
        new ExpectedArgs("party_string_id", "The party string id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.GetPeacePursuitState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ITestPeaceStopsPursuitCommand : ICoopCommand
{
}

public sealed class TestPeaceStopsPursuitCommand : ITestPeaceStopsPursuitCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public TestPeaceStopsPursuitCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "test_peace_stops_pursuit";

    public string Description => "Runs the test peace stops pursuit debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
        new ExpectedArgs("party_string_id", "The party string id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.TestPeaceStopsPursuit(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IKillRandomTroopCommand : ICoopCommand
{
}

public sealed class KillRandomTroopCommand : IKillRandomTroopCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public KillRandomTroopCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "kill_random_troop";

    public string Description => "Runs the kill random troop debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.KillRandomTroop(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IKillAllButOneCommand : ICoopCommand
{
}

public sealed class KillAllButOneCommand : IKillAllButOneCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public KillAllButOneCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "kill_all_but_one";

    public string Description => "Runs the kill all but one debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.KillAllButOneTroop(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IListPlayerEncounterCommand : ICoopCommand
{
}

public sealed class ListPlayerEncounterCommand : IListPlayerEncounterCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public ListPlayerEncounterCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "list_player_encounter";

    public string Description => "Reports player encounter.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.ListPlayerEncounter(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IEncounterStateCommand : ICoopCommand
{
}

public sealed class EncounterStateCommand : IEncounterStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public EncounterStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "encounter_state";

    public string Description => "Reports encounter state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.EncounterState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IRetreatConfirmationCommand : ICoopCommand
{
}

public sealed class RetreatConfirmationCommand : IRetreatConfirmationCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public RetreatConfirmationCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "retreat_confirmation";

    public string Description => "Runs the retreat confirmation debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "The action.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.RetreatConfirmation(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ICompleteEncounterMeetingCommand : ICoopCommand
{
}

public sealed class CompleteEncounterMeetingCommand : ICompleteEncounterMeetingCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public CompleteEncounterMeetingCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "complete_encounter_meeting";

    public string Description => "Runs the complete encounter meeting debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.CompleteEncounterMeeting(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IChooseBattleModeCommand : ICoopCommand
{
}

public sealed class ChooseBattleModeCommand : IChooseBattleModeCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public ChooseBattleModeCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "choose_battle_mode";

    public string Description => "Runs the choose battle mode debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mode", "The battle mode.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.ChooseBattleMode(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IGetEventsCommand : ICoopCommand
{
}

public sealed class GetEventsCommand : IGetEventsCommand
{
    public GetEventsCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "get_events";

    public string Description => "Reports events.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        return global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.GetEvents(new List<string>(args));
    }
}

public interface IGetEventCommand : ICoopCommand
{
}

public sealed class GetEventCommand : IGetEventCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public GetEventCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "get_event";

    public string Description => "Reports event.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("map_event_id", "The map event id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Villages.Commands.MapEventDebugCommands.GetEvent(new List<string>(args));
        return resultFactory.FromOutput(output, "Map event id:");
    }
}

#if DEBUG
public interface IPostBattleFreezeFixtureStartCommand : ICoopCommand
{
}

public sealed class PostBattleFreezeFixtureStartCommand : IPostBattleFreezeFixtureStartCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PostBattleFreezeFixtureStartCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "post_battle_freeze_fixture_start";

    public string Description => "Runs the post battle freeze fixture start debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("first_controller_id", "The first controller id.", true),
        new ExpectedArgs("second_controller_id", "The second controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.PostBattleFreezeFixtureCommands.StartFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPostBattleFreezeFixtureOpenCommand : ICoopCommand
{
}

public sealed class PostBattleFreezeFixtureOpenCommand : IPostBattleFreezeFixtureOpenCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PostBattleFreezeFixtureOpenCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "post_battle_freeze_fixture_open";

    public string Description => "Runs the post battle freeze fixture open debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.PostBattleFreezeFixtureCommands.OpenFixtureEncounters(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPostBattleFreezeFixtureStateCommand : ICoopCommand
{
}

public sealed class PostBattleFreezeFixtureStateCommand : IPostBattleFreezeFixtureStateCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PostBattleFreezeFixtureStateCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "post_battle_freeze_fixture_state";

    public string Description => "Reports post battle freeze fixture state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.PostBattleFreezeFixtureCommands.GetFixtureState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPostBattleFreezeFixtureUnpauseCommand : ICoopCommand
{
}

public sealed class PostBattleFreezeFixtureUnpauseCommand : IPostBattleFreezeFixtureUnpauseCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PostBattleFreezeFixtureUnpauseCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "post_battle_freeze_fixture_unpause";

    public string Description => "Runs the post battle freeze fixture unpause debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.PostBattleFreezeFixtureCommands.UnpauseFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IPostBattleFreezeFixtureRestoreCommand : ICoopCommand
{
}

public sealed class PostBattleFreezeFixtureRestoreCommand : IPostBattleFreezeFixtureRestoreCommand
{
    private readonly IMapEventLegacyCommandResult resultFactory;

    public PostBattleFreezeFixtureRestoreCommand(IMapEventLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.map_event";

    public string Name => "post_battle_freeze_fixture_restore";

    public string Description => "Restores or clears post battle freeze fixture restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.MapEvents.Commands.PostBattleFreezeFixtureCommands.RestoreFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif
