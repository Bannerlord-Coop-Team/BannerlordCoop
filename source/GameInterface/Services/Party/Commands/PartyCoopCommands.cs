using Common.Commands;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Party.Commands;

public interface IPartyLegacyCommandResult
{
    CoopCommandResult FromOutput(string output);

    CoopCommandResult FromOutput(string output, string failurePrefix);
}

public sealed class PartyLegacyCommandResult : IPartyLegacyCommandResult
{
    public CoopCommandResult FromOutput(string output)
    {
        return FromOutput(output, null);
    }

    public CoopCommandResult FromOutput(string output, string failurePrefix)
    {
        if (output == null) return new CoopCommandResult(false, "Command returned no output.", "command_failed");

        bool isKnownFailure = failurePrefix != null && output.StartsWith(failurePrefix, StringComparison.Ordinal);
        bool succeeded = !isKnownFailure && !LooksLikeFailure(output);
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

        return output.IndexOf("_REJECTED", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf("_NOT_COMMITTED", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" is not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" does not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" did not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" required", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" must ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" is already ", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

public interface IGarrisonXpFixtureCaptureCommand : ICoopCommand
{
}

public sealed class GarrisonXpFixtureCaptureCommand : IGarrisonXpFixtureCaptureCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public GarrisonXpFixtureCaptureCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "garrison_xp_fixture_capture";

    public string Description => "Runs the garrison xp fixture capture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.GarrisonTroopXpFixtureCommands.Capture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IGarrisonXpFixtureSetupCommand : ICoopCommand
{
}

public sealed class GarrisonXpFixtureSetupCommand : IGarrisonXpFixtureSetupCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public GarrisonXpFixtureSetupCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "garrison_xp_fixture_setup";

    public string Description => "Runs the garrison xp fixture setup debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
        new ExpectedArgs("player_party_id", "The player party id.", true),
        new ExpectedArgs("garrison_party_id", "The garrison party id.", true),
        new ExpectedArgs("original_owner_hero_id", "The original owner hero id.", true),
        new ExpectedArgs("original_settlement_id", "The original settlement id.", true),
        new ExpectedArgs("original_position_x", "The original position x.", true),
        new ExpectedArgs("original_position_y", "The original position y.", true),
        new ExpectedArgs("original_position_is_on_land", "The original position is on land.", true),
        new ExpectedArgs("garrison_exists", "The garrison exists.", true),
        new ExpectedArgs("garrison_count", "The garrison count.", true),
        new ExpectedArgs("garrison_wounded", "The garrison wounded.", true),
        new ExpectedArgs("garrison_xp", "The garrison xp.", true),
        new ExpectedArgs("player_exists", "The player exists.", true),
        new ExpectedArgs("player_count", "The player count.", true),
        new ExpectedArgs("player_wounded", "The player wounded.", true),
        new ExpectedArgs("player_xp", "The player xp.", true),
        new ExpectedArgs("upgrade_xp", "The upgrade xp.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.GarrisonTroopXpFixtureCommands.Setup(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IGarrisonXpFixtureStateCommand : ICoopCommand
{
}

public sealed class GarrisonXpFixtureStateCommand : IGarrisonXpFixtureStateCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public GarrisonXpFixtureStateCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "garrison_xp_fixture_state";

    public string Description => "Reports garrison xp fixture state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("player_party_id", "The player party id.", true),
        new ExpectedArgs("garrison_party_id", "The garrison party id.", true),
        new ExpectedArgs("character_id", "The character id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.GarrisonTroopXpFixtureCommands.State(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IOpenGarrisonXpFixtureCommand : ICoopCommand
{
}

public sealed class OpenGarrisonXpFixtureCommand : IOpenGarrisonXpFixtureCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public OpenGarrisonXpFixtureCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "open_garrison_xp_fixture";

    public string Description => "Runs the open garrison xp fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("garrison_party_id", "The garrison party id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.GarrisonTroopXpFixtureCommands.OpenGarrison(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IGarrisonXpFixtureScreenStateCommand : ICoopCommand
{
}

public sealed class GarrisonXpFixtureScreenStateCommand : IGarrisonXpFixtureScreenStateCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public GarrisonXpFixtureScreenStateCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "garrison_xp_fixture_screen_state";

    public string Description => "Reports garrison xp fixture screen state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("garrison_party_id", "The garrison party id.", true),
        new ExpectedArgs("character_id", "The character id.", true),
        new ExpectedArgs("expected_state", "The expected screen state.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.GarrisonTroopXpFixtureCommands.ScreenState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IStageGarrisonXpWithdrawalCommand : ICoopCommand
{
}

public sealed class StageGarrisonXpWithdrawalCommand : IStageGarrisonXpWithdrawalCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public StageGarrisonXpWithdrawalCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "stage_garrison_xp_withdrawal";

    public string Description => "Runs the stage garrison xp withdrawal debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("garrison_party_id", "The garrison party id.", true),
        new ExpectedArgs("character_id", "The character id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.GarrisonTroopXpFixtureCommands.StageWithdrawal(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ICommitGarrisonXpWithdrawalCommand : ICoopCommand
{
}

public sealed class CommitGarrisonXpWithdrawalCommand : ICommitGarrisonXpWithdrawalCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public CommitGarrisonXpWithdrawalCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "commit_garrison_xp_withdrawal";

    public string Description => "Runs the commit garrison xp withdrawal debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.GarrisonTroopXpFixtureCommands.CommitWithdrawal(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IGarrisonXpFixtureRestoreCommand : ICoopCommand
{
}

public sealed class GarrisonXpFixtureRestoreCommand : IGarrisonXpFixtureRestoreCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public GarrisonXpFixtureRestoreCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "garrison_xp_fixture_restore";

    public string Description => "Restores or clears garrison xp fixture restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.GarrisonTroopXpFixtureCommands.Restore(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IGarrisonXpFixtureVerifyRestoreCommand : ICoopCommand
{
}

public sealed class GarrisonXpFixtureVerifyRestoreCommand : IGarrisonXpFixtureVerifyRestoreCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public GarrisonXpFixtureVerifyRestoreCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "garrison_xp_fixture_verify_restore";

    public string Description => "Restores or clears garrison xp fixture verify restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.GarrisonTroopXpFixtureCommands.VerifyRestore(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

#if DEBUG
public interface ILargeBattleRosterBeginCommand : ICoopCommand
{
}

public sealed class LargeBattleRosterBeginCommand : ILargeBattleRosterBeginCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public LargeBattleRosterBeginCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "large_battle_roster_begin";

    public string Description => "Runs the large battle roster begin debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("first_party_or_controller_id", "The first party or controller id.", true),
        new ExpectedArgs("second_party_or_controller_id", "The second party or controller id.", true),
        new ExpectedArgs("troops_per_party", "The troops per party.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.LargeBattleRosterFixtureCommands.Begin(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IExactBattleRosterBeginCommand : ICoopCommand
{
}

public sealed class ExactBattleRosterBeginCommand : IExactBattleRosterBeginCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ExactBattleRosterBeginCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "exact_battle_roster_begin";

    public string Description => "Runs the exact battle roster begin debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("first_party_or_controller_id", "The first party or controller id.", true),
        new ExpectedArgs("second_party_or_controller_id", "The second party or controller id.", true),
        new ExpectedArgs("healthy_per_party", "The healthy per party.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.LargeBattleRosterFixtureCommands.BeginExact(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IBattleSizeRosterBeginCommand : ICoopCommand
{
}

public sealed class BattleSizeRosterBeginCommand : IBattleSizeRosterBeginCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public BattleSizeRosterBeginCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "battle_size_roster_begin";

    public string Description => "Runs the battle size roster begin debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("first_party_or_controller_id", "The first party or controller id.", true),
        new ExpectedArgs("second_party_or_controller_id", "The second party or controller id.", true),
        new ExpectedArgs("first_healthy", "The first healthy.", true),
        new ExpectedArgs("second_healthy", "The second healthy.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.LargeBattleRosterFixtureCommands.BeginBattleSize(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface ILargeBattleRosterStatusCommand : ICoopCommand
{
}

public sealed class LargeBattleRosterStatusCommand : ILargeBattleRosterStatusCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public LargeBattleRosterStatusCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "large_battle_roster_status";

    public string Description => "Reports large battle roster status.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("first_party_or_controller_id", "The first party or controller id.", true),
        new ExpectedArgs("second_party_or_controller_id", "The second party or controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.LargeBattleRosterFixtureCommands.Status(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IExactBattleRosterStatusCommand : ICoopCommand
{
}

public sealed class ExactBattleRosterStatusCommand : IExactBattleRosterStatusCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ExactBattleRosterStatusCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "exact_battle_roster_status";

    public string Description => "Reports exact battle roster status.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("first_party_or_controller_id", "The first party or controller id.", true),
        new ExpectedArgs("second_party_or_controller_id", "The second party or controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.LargeBattleRosterFixtureCommands.ExactStatus(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface ILargeBattleRosterRestoreCommand : ICoopCommand
{
}

public sealed class LargeBattleRosterRestoreCommand : ILargeBattleRosterRestoreCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public LargeBattleRosterRestoreCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "large_battle_roster_restore";

    public string Description => "Restores or clears large battle roster restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.LargeBattleRosterFixtureCommands.Restore(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IExactBattleRosterRestoreCommand : ICoopCommand
{
}

public sealed class ExactBattleRosterRestoreCommand : IExactBattleRosterRestoreCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ExactBattleRosterRestoreCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "exact_battle_roster_restore";

    public string Description => "Restores or clears exact battle roster restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.LargeBattleRosterFixtureCommands.RestoreExact(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

public interface IWhoAmICommand : ICoopCommand
{
}

public sealed class WhoAmICommand : IWhoAmICommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public WhoAmICommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "who_am_i";

    public string Description => "Runs the whoami debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.WhoAmICommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IPositionCommand : ICoopCommand
{
}

public sealed class PositionCommand : IPositionCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public PositionCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "position";

    public string Description => "Runs the position debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "The party id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.PositionCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IMoveOffsetCommand : ICoopCommand
{
}

public sealed class MoveOffsetCommand : IMoveOffsetCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public MoveOffsetCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "move_offset";

    public string Description => "Runs the move offset debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("offset_x", "The offset x.", true),
        new ExpectedArgs("offset_y", "The offset y.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.MoveOffsetCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IRestorePositionCommand : ICoopCommand
{
}

public sealed class RestorePositionCommand : IRestorePositionCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public RestorePositionCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "restore_position";

    public string Description => "Restores or clears restore position.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "The party id.", true),
        new ExpectedArgs("x", "The x.", true),
        new ExpectedArgs("y", "The y.", true),
        new ExpectedArgs("is_on_land", "The is on land.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.RestorePositionCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IMoveToSettlementCommand : ICoopCommand
{
}

public sealed class MoveToSettlementCommand : IMoveToSettlementCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public MoveToSettlementCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "move_to_settlement";

    public string Description => "Runs the move to settlement debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "The party id.", true),
        new ExpectedArgs("settlement_id", "The settlement id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.MoveToSettlementCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ICharacterIdsCommand : ICoopCommand
{
}

public sealed class CharacterIdsCommand : ICharacterIdsCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public CharacterIdsCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "character_ids";

    public string Description => "Runs the characterids debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name_or_id", "The hero name or id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.ViewItemIdsCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ISetTroopWoundedCommand : ICoopCommand
{
}

public sealed class SetTroopWoundedCommand : ISetTroopWoundedCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public SetTroopWoundedCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "set_troop_wounded";

    public string Description => "Runs the set troop wounded debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "The party id.", true),
        new ExpectedArgs("character_id", "The character id.", true),
        new ExpectedArgs("wounded_count", "The wounded count.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.SetTroopWoundedCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ISetTroopStateCommand : ICoopCommand
{
}

public sealed class SetTroopStateCommand : ISetTroopStateCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public SetTroopStateCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "set_troop_state";

    public string Description => "Reports set troop state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "The party id.", true),
        new ExpectedArgs("character_id", "The character id.", true),
        new ExpectedArgs("exists", "The exists.", true),
        new ExpectedArgs("number", "The number.", true),
        new ExpectedArgs("wounded_count", "The wounded count.", true),
        new ExpectedArgs("xp", "The xp.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.SetTroopStateCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ISelectPartyScreenTroopCommand : ICoopCommand
{
}

public sealed class SelectPartyScreenTroopCommand : ISelectPartyScreenTroopCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public SelectPartyScreenTroopCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "select_party_screen_troop";

    public string Description => "Runs the select party screen troop debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("character_id", "The character id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.SelectPartyScreenTroopCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IUpgradePartyScreenTroopCommand : ICoopCommand
{
}

public sealed class UpgradePartyScreenTroopCommand : IUpgradePartyScreenTroopCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public UpgradePartyScreenTroopCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "upgrade_party_screen_troop";

    public string Description => "Runs the upgrade party screen troop debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("character_id", "The character id.", true),
        new ExpectedArgs("upgrade_target_index", "The upgrade target index.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.UpgradePartyScreenTroopCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IStagePartyScreenTransferCommand : ICoopCommand
{
}

public sealed class StagePartyScreenTransferCommand : IStagePartyScreenTransferCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public StagePartyScreenTransferCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "stage_party_screen_transfer";

    public string Description => "Runs the stage party screen transfer debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("character_id", "The character id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.StagePartyScreenTransferCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IPartyScreenTroopStateCommand : ICoopCommand
{
}

public sealed class PartyScreenTroopStateCommand : IPartyScreenTroopStateCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public PartyScreenTroopStateCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "party_screen_troop_state";

    public string Description => "Reports party screen troop state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("character_id", "The character id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.PartyScreenTroopStateCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IAddTroopXpCommand : ICoopCommand
{
}

public sealed class AddTroopXpCommand : IAddTroopXpCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public AddTroopXpCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "add_troop_xp";

    public string Description => "Runs the addtroopxp debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The hero name.", true),
        new ExpectedArgs("xp_amount", "The xp amount.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.AddTroopXpCommand(new List<string>(args));
        return resultFactory.FromOutput(output, "Please enter an integer");
    }
}

public interface IAddTroopsCommand : ICoopCommand
{
}

public sealed class AddTroopsCommand : IAddTroopsCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public AddTroopsCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "add_troops";

    public string Description => "Runs the addtroops debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The hero name.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.AddRecruitsCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ISiegeBuffCommand : ICoopCommand
{
}

public sealed class SiegeBuffCommand : ISiegeBuffCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public SiegeBuffCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "siege_buff";

    public string Description => "Runs the siege buff debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "The party id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.SiegeBuffCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IDeclareWarCommand : ICoopCommand
{
}

public sealed class DeclareWarCommand : IDeclareWarCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public DeclareWarCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "declare_war";

    public string Description => "Runs the declare war debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "The party id.", true),
        new ExpectedArgs("settlement_id", "The settlement id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.DeclareWarCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IAddPrisonersCommand : ICoopCommand
{
}

public sealed class AddPrisonersCommand : IAddPrisonersCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public AddPrisonersCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "add_prisoners";

    public string Description => "Runs the addprisoners debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The hero name.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.AddPrisonersCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IRemovePrisonersCommand : ICoopCommand
{
}

public sealed class RemovePrisonersCommand : IRemovePrisonersCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public RemovePrisonersCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "remove_prisoners";

    public string Description => "Runs the removeprisoners debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name", "The hero name.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.RemovePrisonersCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IImprisonCompanionCommand : ICoopCommand
{
}

public sealed class ImprisonCompanionCommand : IImprisonCompanionCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ImprisonCompanionCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "imprison_companion";

    public string Description => "Runs the imprison companion debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("captor_hero", "The captor hero.", true),
        new ExpectedArgs("prisoner_hero", "The prisoner hero.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.ImprisonCompanionCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ISnapshotPrisonCommand : ICoopCommand
{
}

public sealed class SnapshotPrisonCommand : ISnapshotPrisonCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public SnapshotPrisonCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "snapshot_prison";

    public string Description => "Runs the snapshot prison debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_name_or_id", "The hero name or id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.PartyCommands.SnapshotPrisonCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IClanPartyXpFixtureCaptureCommand : ICoopCommand
{
}

public sealed class ClanPartyXpFixtureCaptureCommand : IClanPartyXpFixtureCaptureCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ClanPartyXpFixtureCaptureCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "clan_party_xp_fixture_capture";

    public string Description => "Runs the clan party xp fixture capture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.TroopXpTransferFixtureCommands.Capture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IClanPartyXpFixtureSetupCommand : ICoopCommand
{
}

public sealed class ClanPartyXpFixtureSetupCommand : IClanPartyXpFixtureSetupCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ClanPartyXpFixtureSetupCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "clan_party_xp_fixture_setup";

    public string Description => "Runs the clan party xp fixture setup debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
        new ExpectedArgs("player_party_id", "The player party id.", true),
        new ExpectedArgs("character_id", "The character id.", true),
        new ExpectedArgs("player_count", "The player count.", true),
        new ExpectedArgs("player_wounded", "The player wounded.", true),
        new ExpectedArgs("player_xp", "The player xp.", true),
        new ExpectedArgs("companion_count", "The companion count.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.TroopXpTransferFixtureCommands.Setup(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IClanPartyXpFixtureStateCommand : ICoopCommand
{
}

public sealed class ClanPartyXpFixtureStateCommand : IClanPartyXpFixtureStateCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ClanPartyXpFixtureStateCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "clan_party_xp_fixture_state";

    public string Description => "Reports clan party xp fixture state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("player_party_id", "The player party id.", true),
        new ExpectedArgs("clan_party_id", "The clan party id.", true),
        new ExpectedArgs("character_id", "The character id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.TroopXpTransferFixtureCommands.State(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IOpenClanPartyTransferCommand : ICoopCommand
{
}

public sealed class OpenClanPartyTransferCommand : IOpenClanPartyTransferCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public OpenClanPartyTransferCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "open_clan_party_transfer";

    public string Description => "Runs the open clan party transfer debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("clan_party_id", "The clan party id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.TroopXpTransferFixtureCommands.OpenPartyScreen(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IStageClanPartyTransferCommand : ICoopCommand
{
}

public sealed class StageClanPartyTransferCommand : IStageClanPartyTransferCommand
{
    public StageClanPartyTransferCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "stage_clan_party_transfer";

    public string Description => "Runs the stage clan party transfer debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("clan_party_id", "The clan party id.", true),
        new ExpectedArgs("character_id", "The character id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        return global::GameInterface.Services.Party.Commands.TroopXpTransferFixtureCommands.StageTransfer(new List<string>(args));
    }
}

public interface IClanPartyTransferScreenStateCommand : ICoopCommand
{
}

public sealed class ClanPartyTransferScreenStateCommand : IClanPartyTransferScreenStateCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ClanPartyTransferScreenStateCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "clan_party_transfer_screen_state";

    public string Description => "Reports clan party transfer screen state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("clan_party_id", "The clan party id.", true),
        new ExpectedArgs("character_id", "The character id.", true),
        new ExpectedArgs("expected_state", "The expected screen state.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.TroopXpTransferFixtureCommands.TransferScreenState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ICommitClanPartyTransferCommand : ICoopCommand
{
}

public sealed class CommitClanPartyTransferCommand : ICommitClanPartyTransferCommand
{
    public CommitClanPartyTransferCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "commit_clan_party_transfer";

    public string Description => "Runs the commit clan party transfer debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        return global::GameInterface.Services.Party.Commands.TroopXpTransferFixtureCommands.CommitTransfer(new List<string>(args));
    }
}

public interface IClanPartyXpFixtureRestoreCommand : ICoopCommand
{
}

public sealed class ClanPartyXpFixtureRestoreCommand : IClanPartyXpFixtureRestoreCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ClanPartyXpFixtureRestoreCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "clan_party_xp_fixture_restore";

    public string Description => "Restores or clears clan party xp fixture restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.TroopXpTransferFixtureCommands.Restore(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IClanPartyXpFixtureVerifyRestoreCommand : ICoopCommand
{
}

public sealed class ClanPartyXpFixtureVerifyRestoreCommand : IClanPartyXpFixtureVerifyRestoreCommand
{
    private readonly IPartyLegacyCommandResult resultFactory;

    public ClanPartyXpFixtureVerifyRestoreCommand(IPartyLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.mobile_party";

    public string Name => "clan_party_xp_fixture_verify_restore";

    public string Description => "Restores or clears clan party xp fixture verify restore.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The controller id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.Party.Commands.TroopXpTransferFixtureCommands.VerifyRestore(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
