using Common.Messaging;
using GameInterface.Services.UI.Messages;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab.Sections;

/// <summary>Edits and publishes the client's local nameplate preference.</summary>
public class PlayerNameplatesSection : CoopOptionsSectionVM
{
    public const string SectionId = "PlayerNameplatesSection";

    private readonly IMessageBroker messageBroker;

    public PlayerNameplatesSection(PlayerNameplatesDisplayMode displayMode, IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        DisplayModeSelector = new SelectorVM<SelectorItemVM>(
            new[] { "Always", "Hold Indicators", "Never" },
            (int)displayMode,
            null);
    }

    public override string Id => SectionId;
    public string TitleText => "Player Nameplates";
    public string DescriptionText => "Show names above allied players in co-op missions.";
    public string DisplayModeText => "Display Mode";

    [DataSourceProperty]
    public SelectorVM<SelectorItemVM> DisplayModeSelector { get; }

    public PlayerNameplatesDisplayMode SelectedDisplayMode =>
        (PlayerNameplatesDisplayMode)DisplayModeSelector.SelectedIndex;

    public override void Apply(string tabId, CoopOptionsData options)
    {
        options.SetSection(
            tabId,
            Id,
            new PlayerNameplatesSectionOptions { DisplayMode = SelectedDisplayMode });
    }

    public override void AfterApply()
    {
        messageBroker.Publish(this, new PlayerNameplateVisibilitySelected(SelectedDisplayMode));
    }
}
