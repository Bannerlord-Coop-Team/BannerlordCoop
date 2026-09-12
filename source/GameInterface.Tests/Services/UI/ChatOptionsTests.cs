using Common.Messaging;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab.Sections;
using GameInterface.Services.UI.Messages;
using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class ChatOptionsTests
{
    [Fact]
    public void ChatOptions_DefaultToEnabled()
    {
        using var messageBroker = new MessageBroker();
        var provider = new ChatOptionsTabProvider();

        var tab = provider.CreateTab(new CoopOptionsData(), messageBroker, _ => { });

        Assert.Equal(ChatOptionsTabProvider.TabName, tab.Name);
        Assert.Equal(ChatOptionsTabProvider.TabId, tab.Id);

        var section = Assert.IsType<ChatSection>(Assert.Single(tab.Sections));
        Assert.Equal(ChatSection.SectionId, section.Id);
        Assert.True(section.ShowChat);
    }

    [Fact]
    public void ChatOptions_Disabled_PersistAndPublishAfterApply()
    {
        var filePath = CreateTempFilePath();

        try
        {
            var store = new CoopOptionsStore(filePath);
            using var messageBroker = new MessageBroker();
            ChatVisibilitySelected? selected = null;
            Action<MessagePayload<ChatVisibilitySelected>> handler = payload => selected = payload.What;
            messageBroker.Subscribe(handler);
            var provider = new ChatOptionsTabProvider();
            var tab = provider.CreateTab(store.LoadOrDefault(), messageBroker, _ => { });
            var section = Assert.IsType<ChatSection>(Assert.Single(tab.Sections));

            section.ShowChat = false;
            var options = store.LoadOrDefault();
            tab.Apply(options);
            store.Save(options);
            tab.AfterApply();

            Assert.False(ChatOptionsTabProvider.GetShowChatOrDefault(store.LoadOrDefault()));
            Assert.True(selected.HasValue);
            Assert.False(selected.Value.ShowChat);
            GC.KeepAlive(handler);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(),
            $"bannerlord-coop-chat-options-{Guid.NewGuid():N}.json");
    }
}
