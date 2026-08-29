using Common.Messaging;
using GameInterface.Services.UI.Messages;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers.BugReportTab.Sections;

/// <summary>Configures visibility of the in-game bug-report button.</summary>
public class BugReportSection : CoopOptionsSectionVM
{
    public const string SectionId = "BugReportButton";

    private readonly IMessageBroker messageBroker;
    private bool showBugReportButton;

    public BugReportSection(bool showBugReportButton, IMessageBroker messageBroker)
    {
        this.showBugReportButton = showBugReportButton;
        this.messageBroker = messageBroker;
    }

    public override string Id => SectionId;
    public string TitleText => "Bug Report";
    public string DescriptionText => "Configure the in-game co-op bug-report button.";
    public string ShowBugReportButtonText => "Show Coop Bug Report Button";

    [DataSourceProperty]
    public bool ShowBugReportButton
    {
        get => showBugReportButton;
        set
        {
            if (showBugReportButton == value) return;

            showBugReportButton = value;
            OnPropertyChanged(nameof(ShowBugReportButton));
        }
    }

    public override void Apply(string tabId, CoopOptionsData options)
    {
        options.SetSection(
            tabId,
            Id,
            new BugReportSectionOptions { ShowBugReportButton = showBugReportButton });
    }

    public override void AfterApply()
    {
        messageBroker.Publish(this, new BugReportVisibilitySelected(showBugReportButton));
    }
}
