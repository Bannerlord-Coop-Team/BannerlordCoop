using TaleWorlds.MountAndBlade;

namespace GameInterface.Utils;

/// <summary>
/// The engine's water clock: lets a synced naval battle make every scene's waves read a time it owns instead of the
/// engine's accumulated clock. Everything does nothing when the native patch could not be installed.
/// </summary>
public interface IWaterClock
{
    /// <summary>Whether the native patch is installed (client, Windows, signature matched exactly once).</summary>
    bool IsInstalled { get; }

    /// <summary>Whether the engine currently reads the owned time instead of its own clock.</summary>
    bool IsOverriding { get; }

    /// <summary>Why the patch is or isn't installed.</summary>
    string Status { get; }

    /// <summary>The water time last written.</summary>
    float WrittenTime { get; }

    /// <summary>Installs the native patch once; client processes only. Returns whether it is installed.</summary>
    bool TryInstall();

    /// <summary>The exact water time vanilla reads for a scene of <paramref name="timeType"/> (an MBCommon.TimeType).</summary>
    bool TryReadVanillaTime(int timeType, out float waterTime);

    /// <summary>[Game thread] Writes <paramref name="waterTime"/>, then makes the engine read it instead of its own clock.</summary>
    bool TryBeginOverride(float waterTime);

    /// <summary>[Game thread] Sets the water time the engine reads on its next scene update.</summary>
    void SetWaterTime(float waterTime);

    /// <summary>[Game thread] Gives the engine its own clock back.</summary>
    void EndOverride();
}

/// <inheritdoc cref="IWaterClock"/>
public class WaterClock : IWaterClock
{
    /// <summary>MBCommon.TimeType.Mission (rglTimer_type Tactical_timer), the clock mission scenes' water reads.</summary>
    public const int MissionTimeType = (int)MBCommon.TimeType.Mission;

    public bool IsInstalled => NativeWaterClockPatch.IsActive;

    public bool IsOverriding => NativeWaterClockPatch.IsRedirected;

    public string Status => NativeWaterClockPatch.Status;

    public float WrittenTime => NativeWaterClockPatch.WrittenTime;

    public bool TryInstall() => NativeWaterClockPatch.TryApply();

    public bool TryReadVanillaTime(int timeType, out float waterTime) =>
        NativeWaterClockPatch.TryReadVanillaTime(timeType, out waterTime);

    public bool TryBeginOverride(float waterTime) => NativeWaterClockPatch.TryRedirect(waterTime);

    public void SetWaterTime(float waterTime) => NativeWaterClockPatch.SetWaterTime(waterTime);

    public void EndOverride() => NativeWaterClockPatch.Restore();
}
