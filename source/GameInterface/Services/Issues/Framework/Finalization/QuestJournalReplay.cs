using System;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>Marks received journal values without suppressing progress derived from other received state.</summary>
internal sealed class QuestJournalReplay : IDisposable
{
    [ThreadStatic]
    private static int depth;

    public static bool IsActive => depth > 0;

    public QuestJournalReplay()
    {
        depth++;
    }

    public void Dispose()
    {
        depth--;
    }
}
