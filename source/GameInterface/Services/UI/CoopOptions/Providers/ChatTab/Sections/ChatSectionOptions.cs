using System.Text.Json.Serialization;

namespace GameInterface.Services.UI.CoopOptions.Providers.ChatTab.Sections;

public class ChatSectionOptions
{
    public const bool DefaultShowChat = true;

    [JsonPropertyName("showChat")]
    public bool? ShowChat { get; set; }

    public bool GetShowChatOrDefault()
    {
        return ShowChat ?? DefaultShowChat;
    }
}
