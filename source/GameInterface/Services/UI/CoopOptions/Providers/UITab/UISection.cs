using Common.Messaging;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab.Sections;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab.Sections;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab.Sections;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab.Sections;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers.UITab;

/// <summary>Edits grouped display settings while retaining their saved keys and notifications.</summary>
public sealed class UISection : CoopOptionsSectionVM
{
    private bool nameplatesAvailable;
    public override string Id => "UISection";
    public string TitleText => "Interface";
    public string DescriptionText => "Choose which overlays appear while playing. Select Apply to save changes.";
    public string OverlaysText => "Overlays";
    public string NameplatesText => "Player nameplates";
    public string PreviewText => "Preview";
    public string KillFeedDescriptionText => "Your kills and your troops' kills use this color for all players.";
    [DataSourceProperty] public ChatSection Chat { get; }
    [DataSourceProperty] public MapTimeSection MapTime { get; }
    [DataSourceProperty] public PlayerNameplatesSection Nameplates { get; }
    [DataSourceProperty] public KillFeedSection KillFeed { get; }

    [DataSourceProperty]
    public bool NameplatesAvailable
    {
        get => nameplatesAvailable;
        set
        {
            if (nameplatesAvailable == value) return;
            nameplatesAvailable = value;
            OnPropertyChanged(nameof(NameplatesAvailable));
        }
    }

    // Loads the existing display editors so saved preferences remain compatible.
    public UISection(CoopOptionsData options, IMessageBroker messageBroker)
    {
        Chat = new ChatSection(ChatOptionsTabProvider.GetShowChatOrDefault(options), messageBroker);
        MapTime = new MapTimeSection(MapTimeOptionsTabProvider.GetShowMapTimeInMissionsOrDefault(options));
        Nameplates = new PlayerNameplatesSection(PlayerNameplatesOptionsTabProvider.GetDisplayModeOrDefault(options), messageBroker);
        KillFeed = new KillFeedSection(KillFeedOptionsTabProvider.GetKillFeedColorOrDefault(options), messageBroker);
    }

    // Saves to the original keys used by overlay services, excluding server-disabled nameplates.
    public override void Apply(string tabId, CoopOptionsData options)
    {
        Chat.Apply(ChatOptionsTabProvider.TabId, options);
        MapTime.Apply(MapTimeOptionsTabProvider.TabId, options);
        KillFeed.Apply(KillFeedOptionsTabProvider.TabId, options);
        if (!NameplatesAvailable) return;
        Nameplates.Apply(PlayerNameplatesOptionsTabProvider.TabId, options);
    }

    // Notifies active overlays after the combined settings have been saved.
    public override void AfterApply()
    {
        Chat.AfterApply();
        MapTime.AfterApply();
        KillFeed.AfterApply();
        if (!NameplatesAvailable) return;
        Nameplates.AfterApply();
    }

    // Releases each nested editor when the options screen closes.
    public override void OnFinalize()
    {
        Chat.OnFinalize();
        MapTime.OnFinalize();
        Nameplates.OnFinalize();
        KillFeed.OnFinalize();
        base.OnFinalize();
    }
}
