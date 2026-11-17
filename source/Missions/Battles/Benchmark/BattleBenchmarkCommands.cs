using Common.BattleBenchmark;
using Common.Commands;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace Missions.Battles.Benchmark;

/// <summary>
/// Console commands that run the battle benchmark in any build: start a fixed measurement window on this process, read
/// its state, stop it early, and fix the mission camera so render cost is comparable between runs.
/// See doc/automated-testing/battle-benchmark.md.
/// </summary>
internal static class BattleBenchmarkCommands
{
    private const string CommandPrefix = "coop.debug.benchmark";

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static string FormatStatus(BattleBenchmarkStatus status) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(status);

    // coop.debug.benchmark.start field-ab-01 120 C:\bench\run01 field
    /// <summary>Starts a measurement window on this process; it writes its JSON report when the window ends.</summary>
    public sealed class StartCoopCommand : ICoopCommand
    {
        private readonly IBattleBenchmarkRecorder recorder;

        public StartCoopCommand(IBattleBenchmarkRecorder recorder)
        {
            this.recorder = recorder;
        }

        public string Prefix => CommandPrefix;

        public string Name => "start";

        public string Description => "Starts a battle benchmark window on this process; the report is written when it ends.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("run_id", "Letters, digits, dots, dashes or underscores; names the report folder.", true),
            new ExpectedArgs("seconds", "The window length in seconds.", true),
            new ExpectedArgs("output_directory", "An absolute folder for the report; defaults to CoopBenchmark next to the game executable.", false),
            new ExpectedArgs("label", "A free-form label stored in the report, such as the scenario name.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
                return Failed("Invalid command argument value.");

            string outputDirectory = args.Count > 2 ? args[2] : null;
            string label = args.Count > 3 ? args[3] : null;
            if (!recorder.TryStart(args[0], label, TimeSpan.FromSeconds(seconds), outputDirectory, out string error))
                return Failed(error);

            return Succeeded($"BATTLE_BENCHMARK_STARTED run={args[0]} seconds={seconds}\n{FormatStatus(recorder.Status)}");
        }
    }

    /// <summary>Reports whether a window is recording and where the last report went.</summary>
    public sealed class StatusCoopCommand : ICoopCommand
    {
        private readonly IBattleBenchmarkRecorder recorder;

        public StatusCoopCommand(IBattleBenchmarkRecorder recorder)
        {
            this.recorder = recorder;
        }

        public string Prefix => CommandPrefix;

        public string Name => "status";

        public string Description => "Reports the battle benchmark state on this process.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            BattleBenchmarkStatus status = recorder.Status;
            return Succeeded($"BATTLE_BENCHMARK_STATUS recording={status.Recording} report={status.LastReportPath ?? "none"}\n" +
                             FormatStatus(status));
        }
    }

    /// <summary>Ends the current window early and writes its report.</summary>
    public sealed class StopCoopCommand : ICoopCommand
    {
        private readonly IBattleBenchmarkRecorder recorder;

        public StopCoopCommand(IBattleBenchmarkRecorder recorder)
        {
            this.recorder = recorder;
        }

        public string Prefix => CommandPrefix;

        public string Name => "stop";

        public string Description => "Ends the battle benchmark window on this process early and writes its report.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            return recorder.TryStop(out string reportPath, out string error)
                ? Succeeded($"BATTLE_BENCHMARK_WRITTEN {reportPath}")
                : Failed(error);
        }
    }

    // coop.debug.benchmark.camera 50 80, then coop.debug.benchmark.camera off
    /// <summary>
    /// Fixes the mission camera beside the battle: level with the midpoint between the two sides' average positions,
    /// <c>distance</c> metres off the line between them and <c>height</c> metres up, looking at the midpoint.
    /// <c>off</c> gives the normal camera back.
    /// </summary>
    public sealed class CameraCoopCommand : ICoopCommand
    {
        internal const float DefaultHeight = 50f;
        internal const float DefaultDistance = 80f;
        internal const string Off = "off";

        // The command registry keeps this instance for the session, so later calls move this camera instead of adding one.
        private Camera camera;

        public string Prefix => CommandPrefix;

        public string Name => "camera";

        public string Description =>
            "Fixes the mission camera beside the battle so render cost is comparable between runs; off gives the normal camera back.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("height", $"Metres above the midpoint, defaults to {DefaultHeight}; or {Off} to give the normal camera back.", false),
            new ExpectedArgs("distance", $"Metres off the line between the sides; defaults to {DefaultDistance}.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (args.Count > 0 && string.Equals(args[0], Off, StringComparison.OrdinalIgnoreCase))
                return Release();

            float height = DefaultHeight;
            float distance = DefaultDistance;
            if ((args.Count > 0 && !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out height)) ||
                (args.Count > 1 && !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out distance)))
            {
                return Failed("Invalid command argument value.");
            }

            Mission mission = Mission.Current;
            if (mission == null) return Failed("No active mission");
            if (!(ScreenManager.TopScreen is MissionScreen missionScreen) || missionScreen.CombatCamera == null)
                return Failed("The mission screen is not active");

            Agent[] attackers = ActiveHumans(mission, BattleSideEnum.Attacker);
            Agent[] defenders = ActiveHumans(mission, BattleSideEnum.Defender);
            if (attackers.Length == 0 || defenders.Length == 0)
                return Failed("Both sides need active agents to place the camera");

            Vec3 attackerCentre = Average(attackers);
            Vec3 defenderCentre = Average(defenders);
            Vec3 target = (attackerCentre + defenderCentre) * 0.5f;
            Vec3 axis = defenderCentre - attackerCentre;
            axis.z = 0f;
            axis = axis.LengthSquared > 0.01f ? axis.NormalizedCopy() : Vec3.Forward;
            var side = new Vec3(-axis.y, axis.x, 0f);
            Vec3 eye = target + (side * distance) + (Vec3.Up * height);

            camera ??= Camera.CreateCamera();
            camera.FillParametersFrom(missionScreen.CombatCamera);
            camera.LookAt(eye, target, Vec3.Up);
            missionScreen.CustomCamera = camera;

            return Succeeded($"BATTLE_BENCHMARK_CAMERA eye={Describe(eye)} target={Describe(target)} " +
                             $"attackers={attackers.Length} defenders={defenders.Length}");
        }

        // Same release as the mount camera in BattleDebugCommands: vanilla skips its own camera update while CustomCamera is set.
        private CoopCommandResult Release()
        {
            if (ReferenceEquals(camera, null)) return Failed("No benchmark camera is set");

            if (ScreenManager.TopScreen is MissionScreen missionScreen && ReferenceEquals(missionScreen.CustomCamera, camera))
                missionScreen.CustomCamera = null;
            camera.ReleaseCamera();
            camera = null;
            return Succeeded("BATTLE_BENCHMARK_CAMERA_RELEASED");
        }

        private static string Describe(Vec3 position) =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.0},{1:0.0},{2:0.0})", position.x, position.y, position.z);

        private static Agent[] ActiveHumans(Mission mission, BattleSideEnum side) =>
            mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman && agent.Team?.Side == side).ToArray();

        private static Vec3 Average(Agent[] agents)
        {
            Vec3 sum = Vec3.Zero;
            foreach (Agent agent in agents)
                sum += agent.Position;
            return sum * (1f / agents.Length);
        }
    }
}
