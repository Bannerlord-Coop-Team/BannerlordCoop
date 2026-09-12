using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.UI.CoopOptions.Providers.BugReportTab.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.BugReportTab;

/// <summary>Provides the client bug-report presentation options tab.</summary>
public class BugReportOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "BugReporting";
    public const string TabName = "Bug Report";

    public string Id => TabId;
    public bool IsAvailable(ModOptions modOptions) => true;

    public CoopOptionsTabVM CreateTab(
        CoopOptionsData options,
        IMessageBroker messageBroker,
        Action<CoopOptionsTabVM> onSelect)
    {
        return new CoopOptionsTabVM(
            Id,
            TabName,
            new CoopOptionsSectionVM[]
            {
                new BugReportSection(GetShowBugReportButtonOrDefault(options), messageBroker)
            },
            onSelect);
    }

    public static bool GetShowBugReportButtonOrDefault(CoopOptionsData options)
    {
        var sectionOptions =
            (options ?? new CoopOptionsData()).GetSectionOrDefault(
                TabId,
                BugReportSection.SectionId,
                new BugReportSectionOptions());
        return sectionOptions.GetShowBugReportButtonOrDefault();
    }
}
