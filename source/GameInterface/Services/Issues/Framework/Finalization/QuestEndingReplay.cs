using System;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>
/// Marks a client replaying the server's ending of a quest, the only time a client may end a registered quest itself.
/// </summary>
internal sealed class QuestEndingReplay : IDisposable
{
    [ThreadStatic]
    private static int depth;

    public static bool IsActive => depth > 0;

    public QuestEndingReplay()
    {
        depth++;
    }

    public void Dispose()
    {
        depth--;
    }
}
