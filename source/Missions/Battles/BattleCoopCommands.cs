using Common.Commands;
using System;
using System.Collections.Generic;

namespace Missions.Battles;

public interface IBattleLegacyCommandResult
{
    CoopCommandResult FromOutput(string output);
}

public sealed class BattleLegacyCommandResult : IBattleLegacyCommandResult
{
    public CoopCommandResult FromOutput(string output)
    {
        if (output == null) return new CoopCommandResult(false, "Command returned no output.", "command_failed");

        bool succeeded = !LooksLikeFailure(output);
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

        return false;
    }
}

#if DEBUG
public interface IReplicationFixtureCommand : ICoopCommand
{
}

public sealed class ReplicationFixtureCommand : IReplicationFixtureCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public ReplicationFixtureCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "replication_fixture";

    public string Description => "Runs the replication fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mode", "The mode.", true),
        new ExpectedArgs("connected_controller_id", "The connected controller id.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.ReplicationFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IColumnReinforcementFixtureCommand : ICoopCommand
{
}

public sealed class ColumnReinforcementFixtureCommand : IColumnReinforcementFixtureCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public ColumnReinforcementFixtureCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "column_reinforcement_fixture";

    public string Description => "Runs the column reinforcement fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "The action.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.ColumnReinforcementFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IActionPerformanceCommand : ICoopCommand
{
}

public sealed class ActionPerformanceCommand : IActionPerformanceCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public ActionPerformanceCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "action_performance";

    public string Description => "Runs the action performance debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "The action.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.ActionPerformance(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IAnimationTraceCommand : ICoopCommand
{
}

public sealed class AnimationTraceCommand : IAnimationTraceCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public AnimationTraceCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "animation_trace";

    public string Description => "Runs the animation trace debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "The action.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.AnimationTrace(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IWieldTestCommand : ICoopCommand
{
}

public sealed class WieldTestCommand : IWieldTestCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public WieldTestCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "wield_test";

    public string Description => "Runs the wield test debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "The action.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.WieldTest(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IItemModifierStateCommand : ICoopCommand
{
}

public sealed class ItemModifierStateCommand : IItemModifierStateCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public ItemModifierStateCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "item_modifier_state";

    public string Description => "Reports item modifier state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.ItemModifierState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

public interface IStateCommand : ICoopCommand
{
}

public sealed class StateCommand : IStateCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public StateCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "state";

    public string Description => "Reports state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.State(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ISizeStateCommand : ICoopCommand
{
}

public sealed class SizeStateCommand : ISizeStateCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public SizeStateCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "size_state";

    public string Description => "Reports size state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.SizeState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IChargeOwnedFormationsCommand : ICoopCommand
{
}

public sealed class ChargeOwnedFormationsCommand : IChargeOwnedFormationsCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public ChargeOwnedFormationsCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "charge_owned_formations";

    public string Description => "Runs the charge owned formations debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.ChargeOwnedFormations(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IMountStateCommand : ICoopCommand
{
}

public sealed class MountStateCommand : IMountStateCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public MountStateCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "mount_state";

    public string Description => "Reports mount state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("authority", "The authority.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.MountState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ICaptureMountPoseCommand : ICoopCommand
{
}

public sealed class CaptureMountPoseCommand : ICaptureMountPoseCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public CaptureMountPoseCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "capture_mount_pose";

    public string Description => "Runs the capture mount pose debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mount_agent_id", "The mount agent id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.CaptureMountPose(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IMountPoseSamplesCommand : ICoopCommand
{
}

public sealed class MountPoseSamplesCommand : IMountPoseSamplesCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public MountPoseSamplesCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "mount_pose_samples";

    public string Description => "Runs the mount pose samples debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mount_agent_id", "The mount agent id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.MountPoseSamplesState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IMoveCavalryCommand : ICoopCommand
{
}

public sealed class MoveCavalryCommand : IMoveCavalryCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public MoveCavalryCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "move_cavalry";

    public string Description => "Runs the move cavalry debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("distance", "The distance.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.MoveCavalry(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IHoldCavalryCommand : ICoopCommand
{
}

public sealed class HoldCavalryCommand : IHoldCavalryCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public HoldCavalryCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "hold_cavalry";

    public string Description => "Runs the hold cavalry debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.HoldCavalry(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ITurnCavalryCommand : ICoopCommand
{
}

public sealed class TurnCavalryCommand : ITurnCavalryCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public TurnCavalryCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "turn_cavalry";

    public string Description => "Runs the turn cavalry debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("degrees", "The degrees.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.TurnCavalry(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IFocusMountCommand : ICoopCommand
{
}

public sealed class FocusMountCommand : IFocusMountCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public FocusMountCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "focus_mount";

    public string Description => "Runs the focus mount debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mount_agent_id", "The mount agent id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.FocusMount(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IMountCameraStateCommand : ICoopCommand
{
}

public sealed class MountCameraStateCommand : IMountCameraStateCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public MountCameraStateCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "mount_camera_state";

    public string Description => "Reports mount camera state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.MountCameraState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IReleaseMountCameraCommand : ICoopCommand
{
}

public sealed class ReleaseMountCameraCommand : IReleaseMountCameraCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public ReleaseMountCameraCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "release_mount_camera";

    public string Description => "Restores or clears release mount camera.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.ReleaseMountCameraCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface ILadderStateCommand : ICoopCommand
{
}

public sealed class LadderStateCommand : ILadderStateCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public LadderStateCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "ladder_state";

    public string Description => "Reports ladder state.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("machine_id", "The machine id.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.LadderState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IFocusLadderCommand : ICoopCommand
{
}

public sealed class FocusLadderCommand : IFocusLadderCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public FocusLadderCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "focus_ladder";

    public string Description => "Runs the focus ladder debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("machine_id", "The machine id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.FocusLadder(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

public interface IReleaseLadderCameraCommand : ICoopCommand
{
}

public sealed class ReleaseLadderCameraCommand : IReleaseLadderCameraCommand
{
    private readonly IBattleLegacyCommandResult resultFactory;

    public ReleaseLadderCameraCommand(IBattleLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.battle";

    public string Name => "release_ladder_camera";

    public string Description => "Restores or clears release ladder camera.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Battles.BattleDebugCommands.ReleaseLadderCameraCommand(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
