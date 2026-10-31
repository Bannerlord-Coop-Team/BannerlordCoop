using GameInterface.Utils;

namespace E2E.Tests.Environment.MockEngine;

/// <summary>
/// An in-memory engine water clock for one client, so a naval battle never touches native memory. Off until a scenario
/// installs it, like a process without the engine DLL; the vanilla mission time only moves through <see cref="Elapse"/>.
/// </summary>
public sealed class FakeWaterClock : IWaterClock
{
    /// <summary>Water units per mission second, as the engine's clock counts them.</summary>
    public const float UnitsPerSecond = 10f;

    /// <summary>Whether the patch counts as installed; checked on every call, as the battle clock reads it.</summary>
    public bool Installed { get; set; }

    /// <summary>The vanilla mission water time this client would read without the redirect.</summary>
    public float Vanilla { get; set; }

    public bool IsInstalled => Installed;

    public bool IsRedirected { get; private set; }

    public string Status => Installed ? "installed (fake)" : "disabled: fake";

    public float WrittenTime { get; private set; }

    /// <summary>Every water time written while redirected, in order.</summary>
    public List<float> Written { get; } = new();

    /// <summary>Advances this client's vanilla mission clock by <paramref name="seconds"/> of mission time.</summary>
    public void Elapse(float seconds) => Vanilla += seconds * UnitsPerSecond;

    public bool TryInstall() => Installed;

    public bool TryReadVanillaMissionTime(out float waterTime)
    {
        waterTime = Vanilla;
        return Installed;
    }

    public bool TryRedirect(float waterTime)
    {
        if (!Installed) return false;

        SetWaterTime(waterTime);
        IsRedirected = true;
        return true;
    }

    public void SetWaterTime(float waterTime)
    {
        if (!Installed) return;

        WrittenTime = waterTime;
        Written.Add(waterTime);
    }

    public void Restore() => IsRedirected = false;
}
