using Common.Messaging;
using GameInterface.Services.UI.Messages;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab.Sections;

/// <summary>Edits and publishes the client's local nameplate preference.</summary>
public class PlayerNameplatesSection : CoopOptionsSectionVM
{
    public const string SectionId = "PlayerNameplatesSection";

    private readonly IMessageBroker messageBroker;
    private bool showPlayerNameplates;

    public PlayerNameplatesSection(bool showPlayerNameplates, IMessageBroker messageBroker)
    {
        this.showPlayerNameplates = showPlayerNameplates;
        this.messageBroker = messageBroker;
    }

    public override string Id => SectionId;
    public string TitleText => "Player Nameplates";
    public string DescriptionText => "Show names above allied players in co-op missions.";
    public string ShowPlayerNameplatesText => "Show Player Nameplates";

    [DataSourceProperty]
    public bool ShowPlayerNameplates
    {
        get => showPlayerNameplates;
        set
        {
            if (showPlayerNameplates == value) return;

            showPlayerNameplates = value;
            OnPropertyChanged(nameof(ShowPlayerNameplates));
        }
    }

    public override void Apply(string tabId, CoopOptionsData options)
    {
        options.SetSection(
            tabId,
            Id,
            new PlayerNameplatesSectionOptions { ShowPlayerNameplates = showPlayerNameplates });
    }

    public override void AfterApply()
    {
        messageBroker.Publish(this, new PlayerNameplateVisibilitySelected(showPlayerNameplates));
    }
}
