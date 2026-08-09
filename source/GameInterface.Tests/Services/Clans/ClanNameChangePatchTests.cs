using Autofac;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

[Collection(ModInformationRoleCollection.Name)]
public class ClanNameChangePatchTests
{
    [Fact]
    public void OriginalAllowed_LeavesCharacterCreationRenameLocal()
    {
        var result = RunPrefix(allowOriginal: true);

        Assert.True(result.RunOriginal);
        Assert.Empty(result.PublishedMessages);
    }

    [Fact]
    public void OriginalDenied_PublishesSynchronizedRenameRequest()
    {
        var result = RunPrefix(allowOriginal: false);

        Assert.False(result.RunOriginal);
        ChangeClanName message = Assert.Single(result.PublishedMessages);
        Assert.Same(result.Clan, message.Clan);
        Assert.Same(result.Name, message.Name);
        Assert.Same(result.Name, message.InformalName);
    }

    private static PrefixResult RunPrefix(bool allowOriginal)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new TestSyncPolicy(allowOriginal)).As<ISyncPolicy>();
        using var container = builder.Build();

        var clan = ObjectHelper.SkipConstructor<Clan>();
        var name = new TextObject("Test Clan");
        var publishedMessages = new List<ChangeClanName>();
        Action<MessagePayload<ChangeClanName>> capture = payload => publishedMessages.Add(payload.What);
        MessageBroker.Instance.Subscribe(capture);

        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                bool runOriginal = ClanNameChangePatch.ChangeClanNamePrefix(clan, name, name);
                return new PrefixResult(runOriginal, publishedMessages, clan, name);
            }
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);

            if (hadPreviousContainer)
            {
                ContainerProvider.SetContainer(previousContainer);
            }
            else
            {
                ContainerProvider.Clear();
            }
        }
    }

    private sealed class TestSyncPolicy : ISyncPolicy
    {
        private readonly bool allowOriginal;

        public TestSyncPolicy(bool allowOriginal)
        {
            this.allowOriginal = allowOriginal;
        }

        public bool AllowOriginal() => allowOriginal;
    }

    private sealed record PrefixResult(
        bool RunOriginal,
        List<ChangeClanName> PublishedMessages,
        Clan Clan,
        TextObject Name);
}
