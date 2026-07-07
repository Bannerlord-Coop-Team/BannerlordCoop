using Common.Messaging;
using GameInterface.Services.UI;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;

public class KillFeedOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "KillFeedTab";
    public const string TabName = "Kill-feed Color";
    public const string SectionTitleText = TabName;
    public const string SectionDescriptionText = "Kills from you and your own troops will show this color in the kill-feed to all players";
    public const string KillFeedColorRedText = "Red";
    public const string KillFeedColorGreenText = "Green";
    public const string KillFeedColorBlueText = "Blue";

    public string Id => TabId;

    public CoopOptionsTabVM CreateTab(CoopOptionsData options, IMessageBroker messageBroker, Action<CoopOptionsTabVM> onSelect)
    {
        return new CoopOptionsTabVM(
            Id,
            TabName,
            new CoopOptionsSectionVM[]
            {
                new KillFeedSection(GetKillFeedColorOrDefault(options), messageBroker)
            },
            onSelect);
    }

    public static PlayerKillFeedColor GetKillFeedColorOrDefault(CoopOptionsData options)
    {
        var sectionOptions = (options ?? new CoopOptionsData()).GetSectionOrDefault(
            TabId,
            KillFeedSection.SectionId,
            new KillFeedSectionOptions());

        return sectionOptions.GetKillFeedColorOrDefault();
    }

    public static bool TryGetKillFeedColor(CoopOptionsData options, out PlayerKillFeedColor color)
    {
        color = default;

        if (options == null) return false;
        if (!options.TryGetSection<KillFeedSectionOptions>(TabId, KillFeedSection.SectionId, out var sectionOptions)) return false;

        return sectionOptions.TryGetKillFeedColor(out color);
    }
}
