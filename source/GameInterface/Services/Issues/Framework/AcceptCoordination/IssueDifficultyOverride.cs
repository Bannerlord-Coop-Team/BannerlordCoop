using System;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// Makes the vanilla issue difficulty multiplier return the accepting player's value on this
/// thread, vanilla gets it from the player's progress and every machine has its own.
/// </summary>
internal sealed class IssueDifficultyOverride : IDisposable
{
    [ThreadStatic]
    private static float? current;

    private readonly float? previous;

    public IssueDifficultyOverride(float multiplier)
    {
        previous = current;
        current = multiplier;
    }

    public static bool TryGet(out float multiplier)
    {
        multiplier = current ?? 0f;
        return current.HasValue;
    }

    public void Dispose()
    {
        current = previous;
    }
}
