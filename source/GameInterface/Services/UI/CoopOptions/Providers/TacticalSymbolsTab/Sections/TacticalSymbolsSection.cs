using Common.Messaging;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.TacticalSymbolsTab;
using GameInterface.Services.UI.Messages;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers.TacticalSymbolsTab.Sections;

public class TacticalSymbolsSection : CoopOptionsSectionVM
{
    public const string SectionId = "TacticalSymbolsSection";

    private readonly IMessageBroker messageBroker;

    private bool hideTacticalUnitSymbols;

    public TacticalSymbolsSection(bool hideTacticalUnitSymbols, IMessageBroker messageBroker)
    {
        this.hideTacticalUnitSymbols = hideTacticalUnitSymbols;
        this.messageBroker = messageBroker;
    }

    public override string Id => SectionId;

    public string TitleText => TacticalSymbolsOptionsTabProvider.SectionTitleText;
    public string DescriptionText => TacticalSymbolsOptionsTabProvider.SectionDescriptionText;
    public string HideTacticalUnitSymbolsText => TacticalSymbolsOptionsTabProvider.HideTacticalUnitSymbolsText;
    public string HideTacticalUnitSymbolsValueText => hideTacticalUnitSymbols ? "On" : "Off";

    [DataSourceProperty]
    public bool HideTacticalUnitSymbols
    {
        get => hideTacticalUnitSymbols;
        private set
        {
            if (hideTacticalUnitSymbols == value) return;

            hideTacticalUnitSymbols = value;
            OnPropertyChanged(nameof(HideTacticalUnitSymbols));
            OnPropertyChanged(nameof(HideTacticalUnitSymbolsValueText));
        }
    }

    public void ExecuteToggleHideTacticalUnitSymbols()
    {
        HideTacticalUnitSymbols = !HideTacticalUnitSymbols;
    }

    public override void Apply(string tabId, CoopOptionsData options)
    {
    }

    public override void AfterApply()
    {
        messageBroker.Publish(this, new TacticalUnitSymbolsVisibilitySelected(HideTacticalUnitSymbols));
    }
}
