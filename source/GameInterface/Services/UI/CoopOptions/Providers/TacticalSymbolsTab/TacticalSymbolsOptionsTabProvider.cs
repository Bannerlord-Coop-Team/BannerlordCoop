using Common.Messaging;
using GameInterface.Services.UI;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.TacticalSymbolsTab.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.TacticalSymbolsTab;

public class TacticalSymbolsOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "TacticalSymbolsTab";
    public const string TabName = "Tactical Symbols";
    public const string SectionTitleText = "Tactical Unit Symbols";
    public const string SectionDescriptionText = "Hide formation symbols for every player while using Alt or the order menu.";
    public const string HideTacticalUnitSymbolsText = "Hide tactical symbols";

    public string Id => TabId;

    public CoopOptionsTabVM CreateTab(CoopOptionsData options, IMessageBroker messageBroker, Action<CoopOptionsTabVM> onSelect)
    {
        return new CoopOptionsTabVM(
            Id,
            TabName,
            new CoopOptionsSectionVM[]
            {
                new TacticalSymbolsSection(TacticalUnitSymbolsSettings.HideTacticalUnitSymbols, messageBroker)
            },
            onSelect,
            persistsOptions: false);
    }
}
