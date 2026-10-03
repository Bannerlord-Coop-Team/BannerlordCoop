using GameInterface.Serialization;
using GameInterface.Services.Issues.Interfaces;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Surrogates;
using Moq;
using ProtoBuf;
using System.IO;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public sealed class ArtisanProductJournalTests
{
    [Fact]
    public void ProgressSnapshotsPreserveReadLogsAndTrackOnlyNewEntries()
    {
        var journal = new ArtisanProductJournal(new BinaryPackageFactory(Mock.Of<IObjectManager>()));
        var first = new JournalLog(new CampaignTime(100), new TextObject("{=artisan_progress}Delivery"),
            new TextObject("{=artisan_goods}Goods"), 0, 20, LogType.Discreate);
        var unread = new List<JournalLog>();
        var original = new[] { first };
        var current = journal.Merge(original, journal.Pack(new[]
        {
            new JournalLog(first.LogTime, first.LogText, first.TaskName, 7, 20, first.Type),
            new JournalLog(new CampaignTime(200), new TextObject("{=artisan_return}Companion returning")),
        }));
        ArtisanProductJournalHandler.UpdateUnread(unread, original, current);
        Assert.Same(first, current[0]);
        Assert.Equal(7, first.CurrentProgress);
        Assert.Same(current[1], Assert.Single(unread));

        var repeated = journal.Merge(current, journal.Pack(current));
        ArtisanProductJournalHandler.UpdateUnread(unread, current, repeated);
        Assert.Same(current[1], Assert.Single(unread));

        var corrected = journal.Merge(repeated, journal.Pack(new[]
        {
            first, new JournalLog(new CampaignTime(200), new TextObject("{=artisan_cancel}Quest cancelled")),
        }));
        ArtisanProductJournalHandler.UpdateUnread(unread, repeated, corrected);
        Assert.NotSame(repeated[1], corrected[1]);
        Assert.Same(corrected[1], Assert.Single(unread));
        ArtisanProductJournalHandler.UpdateUnread(unread, corrected, original);
        Assert.Empty(unread);
    }

    [Fact]
    public void CompletedJournalOwnersSurviveIssueCleanupAndRegistryRestore()
    {
        var registry = new IssueOwnershipRegistry();
        var first = ObjectHelper.SkipConstructor<JournalLogEntry>();
        var second = ObjectHelper.SkipConstructor<JournalLogEntry>();
        registry.SetJournalOwner(first, "player-A");
        registry.SetJournalOwner(second, "player-B");
        registry.Clear(ObjectHelper.SkipConstructor<Hero>());
        var saved = registry.JournalSnapshot();
        registry.ClearAll();
        Assert.False(registry.TryGetJournalOwner(first, out _));
        registry.RestoreJournalOwners(saved);
        Assert.True(registry.TryGetJournalOwner(first, out var firstOwner));
        Assert.Equal("player-A", firstOwner);
        Assert.True(registry.TryGetJournalOwner(second, out var secondOwner));
        Assert.Equal("player-B", secondOwner);
        registry.RestoreJournalOwners(null);
        Assert.Empty(registry.JournalSnapshot());
    }

    [Fact]
    public void TerminalSnapshotRetainsLocalizedLogsAndCompletedDeliveryAcrossWire()
    {
        _ = new SurrogateCollection();
        var journal = new ArtisanProductJournal(new BinaryPackageFactory(Mock.Of<IObjectManager>()));
        var text = new TextObject("{=artisan_test}Delivered {AMOUNT} goods");
        text.SetTextVariable("AMOUNT", 17);
        var task = new TextObject("{=artisan_task}Products delivered");
        var entries = journal.Pack(new[]
        {
            new JournalLog(new CampaignTime(12345), text, task, 17, 17, LogType.Discreate),
            new JournalLog(new CampaignTime(23456), new TextObject("{=artisan_done}Sale complete"), null, 0, 0, LogType.Text),
        });
        var sent = new NetworkArtisanProductJournal("giver", 8, null, entries, 0, 17, true,
            IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess, QuestBase.QuestCompleteDetails.Success, true, true);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, sent);
        stream.Position = 0;
        var received = Serializer.Deserialize<NetworkArtisanProductJournal>(stream);
        var logs = journal.Unpack(received.QuestEntries);

        Assert.Equal("giver", received.GiverId);
        Assert.Equal(8, received.Generation);
        Assert.Equal(0, received.DeliveryLogIndex);
        Assert.Equal(17, received.Delivered);
        Assert.True(received.MerchantRefused);
        Assert.True(received.MerchantOfferGiven);
        Assert.True(received.IssueEffectsResolved);
        Assert.Equal(IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess, received.IssueStatus);
        Assert.Equal(QuestBase.QuestCompleteDetails.Success, received.QuestStatus);
        Assert.Equal(2, logs.Length);
        Assert.Equal(12345, logs[0].LogTime.NumTicks);
        Assert.Equal(text.Value, logs[0].LogText.Value);
        Assert.Equal(text.ToString(), logs[0].LogText.ToString());
        Assert.Equal(task.Value, logs[0].TaskName.Value);
        Assert.Equal(17, logs[0].CurrentProgress);
        Assert.Equal(17, logs[0].Range);
        Assert.Equal(LogType.Discreate, logs[0].Type);
        Assert.Equal(23456, logs[1].LogTime.NumTicks);
        Assert.Null(logs[1].TaskName);
        Assert.Equal(LogType.Text, logs[1].Type);
        Assert.Empty(journal.Unpack(received.IssueEntries));
    }
}
