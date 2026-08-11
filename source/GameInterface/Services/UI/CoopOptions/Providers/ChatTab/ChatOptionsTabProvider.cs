using Common.Messaging;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.ChatTab;

public class ChatOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "ChatTab";
    public const string TabName = "Chat";

    public string Id => TabId;

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
                new ChatSection(GetShowChatOrDefault(options), messageBroker)
            },
            onSelect);
    }

    public static bool GetShowChatOrDefault(CoopOptionsData options)
    {
        var sectionOptions =
            (options ?? new CoopOptionsData()).GetSectionOrDefault(
                TabId,
                ChatSection.SectionId,
                new ChatSectionOptions());
        return sectionOptions.GetShowChatOrDefault();
    }
}
