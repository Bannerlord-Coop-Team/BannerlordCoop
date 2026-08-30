using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

/// <summary>Notifies the active client overlay of its saved button visibility choice.</summary>
public readonly struct BugReportVisibilitySelected : IEvent
{
    public readonly bool ShowBugReportButton;

    public BugReportVisibilitySelected(bool showBugReportButton)
    {
        ShowBugReportButton = showBugReportButton;
    }
}
