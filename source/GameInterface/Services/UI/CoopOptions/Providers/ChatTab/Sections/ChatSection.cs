using Common.Messaging;
using GameInterface.Services.UI.Messages;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers.ChatTab.Sections;

public class ChatSection : CoopOptionsSectionVM
{
    public const string SectionId = "ChatSection";

    private readonly IMessageBroker messageBroker;
    private bool showChat;

    public ChatSection(bool showChat, IMessageBroker messageBroker)
    {
        this.showChat = showChat;
        this.messageBroker = messageBroker;
    }

    public override string Id => SectionId;
    public string TitleText => "Chat";
    public string DescriptionText => "Configure the in-game co-op chat overlay.";
    public string ShowChatText => "Show Chat";

    [DataSourceProperty]
    public bool ShowChat
    {
        get => showChat;
        set
        {
            if (showChat == value) return;

            showChat = value;
            OnPropertyChanged(nameof(ShowChat));
        }
    }

    public override void Apply(string tabId, CoopOptionsData options)
    {
        options.SetSection(
            tabId,
            Id,
            new ChatSectionOptions { ShowChat = showChat });
    }

    public override void AfterApply()
    {
        messageBroker.Publish(this, new ChatVisibilitySelected(showChat));
    }
}
