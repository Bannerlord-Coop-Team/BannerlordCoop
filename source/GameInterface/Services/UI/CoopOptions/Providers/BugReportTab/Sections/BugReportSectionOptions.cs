namespace GameInterface.Services.UI.CoopOptions.Providers.BugReportTab.Sections;

/// <summary>Persists the client's bug-report button visibility choice.</summary>
public class BugReportSectionOptions
{
    public const bool DefaultShowBugReportButton = true;

    public bool? ShowBugReportButton { get; set; }

    public bool GetShowBugReportButtonOrDefault()
    {
        return ShowBugReportButton ?? DefaultShowBugReportButton;
    }
}
