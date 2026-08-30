namespace GameInterface.Services.BugReporting;

/// <summary>Controls optional diagnostic bug-report triggers.</summary>
internal static class BugReportConfig
{
    /// <summary>Enables automatic bug reports after the unstuck command runs.</summary>
    public static bool UnstuckCommandReportsEnabled = false;
}
