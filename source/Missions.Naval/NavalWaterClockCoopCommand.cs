#if DEBUG
using Common.Commands;
using GameInterface.Utils;
using Missions.Battles;
using System;
using System.Globalization;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.water_clock
/// <summary>DEBUG: reports the native water clock patch and, in a coop naval battle, the shared battle water time.</summary>
public sealed class NavalWaterClockCoopCommand : ICoopCommand
{
    private readonly IWaterClock waterClock;

    public NavalWaterClockCoopCommand(IWaterClock waterClock)
    {
        this.waterClock = waterClock;
    }

    public string Prefix => "coop.debug.naval";

    public string Name => "water_clock";

    public string Description => "Reports the water clock patch status, the water time written this frame, the vanilla mission " +
        "water time and the offset between them; in a coop naval battle also the host role and the offset estimate.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    // Reports the patch and, in a naval battle, the battle water clock as one NAVAL_WATER_CLOCK line.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args != null && args.Count > 0) return new CoopCommandResult(false, "Usage: coop.debug.naval.water_clock", "command_failed");

        var battle = Mission.Current?.GetMissionBehavior<CoopNavalBattleBehavior>()?.WaterClock;
        return new CoopCommandResult(true, Describe(waterClock, battle));
    }

    /// <summary>The NAVAL_WATER_CLOCK line for a water clock and the battle clock driving it, if any.</summary>
    internal static string Describe(IWaterClock waterClock, IBattleWaterClock battle)
    {
        string line = $"NAVAL_WATER_CLOCK installed={Bool(waterClock.IsInstalled)} overriding={Bool(waterClock.IsOverriding)} " +
            $"status=\"{waterClock.Status}\"";
        if (waterClock.TryReadVanillaMissionTime(out var vanilla))
        {
            line += $" written={Number(waterClock.WrittenTime)} vanilla={Number(vanilla)}" +
                $" offset={Number(waterClock.WrittenTime - vanilla)}";
        }
        if (battle == null) return line + " battle=none";

        var estimator = battle.Estimator;
        return line + $" battle=naval host={Bool(battle.IsHost)} hostTime={Bool(estimator.HasHostTime)} samples={battle.SamplesAccepted}" +
            $" target={Number(estimator.TargetOffset)} applied={Number(estimator.AppliedOffset)}";
    }

    // Lower-case true/false for log parsers.
    private static string Bool(bool value) => value ? "true" : "false";

    // A water time or offset with three decimals, invariant culture.
    private static string Number(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
}
#endif
