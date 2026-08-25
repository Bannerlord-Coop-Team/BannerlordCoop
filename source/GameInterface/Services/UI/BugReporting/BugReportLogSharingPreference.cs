using GameInterface.Services.UI.CoopOptions;
using System;

namespace GameInterface.Services.UI.BugReporting;

/// <summary>Provides the client's saved diagnostic bug-report log-sharing choice.</summary>
public interface IBugReportLogSharingPreference
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

/// <inheritdoc />
public class BugReportLogSharingPreference : IBugReportLogSharingPreference
{
    private readonly ICoopOptionsStore optionsStore;

    public BugReportLogSharingPreference(ICoopOptionsStore optionsStore)
    {
        if (optionsStore == null) throw new ArgumentNullException(nameof(optionsStore));
        this.optionsStore = optionsStore;
    }

    public bool IsEnabled()
    {
        var options = optionsStore.LoadOrDefault();
        return options.TryGetSection(
                   BugReportConsentCoordinator.TabId,
                   BugReportConsentCoordinator.SectionId,
                   out BugReportConsentOptions saved) &&
               saved.ShareBugReportLogs == true &&
               saved.DisclosureVersion == BugReportConsentCoordinator.CurrentDisclosureVersion;
    }

    public void SetEnabled(bool enabled)
    {
        var options = optionsStore.LoadOrDefault();
        options.SetSection(
            BugReportConsentCoordinator.TabId,
            BugReportConsentCoordinator.SectionId,
            new BugReportConsentOptions
            {
                ShareBugReportLogs = enabled,
                DisclosureVersion = BugReportConsentCoordinator.CurrentDisclosureVersion,
            });
        optionsStore.Save(options);
    }
}
