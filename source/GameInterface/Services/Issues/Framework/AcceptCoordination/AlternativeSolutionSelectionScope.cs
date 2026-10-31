namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>Marks the native client alternative-selection consequence while it stages its local picker roster.</summary>
internal sealed class AlternativeSolutionSelectionScope : System.IDisposable
{
    [System.ThreadStatic]
    private static AlternativeSolutionSelectionScope current;

    private readonly AlternativeSolutionSelectionScope previous;

    public AlternativeSolutionSelectionScope()
    {
        previous = current;
        current = this;
    }

    public static bool IsActive => current != null;

    public void Dispose()
    {
        if (ReferenceEquals(current, this)) current = previous;
    }
}
