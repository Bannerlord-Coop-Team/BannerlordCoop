using Common.Util;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Surrogates;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

public class ArmyNeedsSuppliesJournalTests
{
    [Fact]
    public void AcceptanceRoundTripPreservesAuthoritativeAmountsDeadlineAndLocalizedLogs()
    {
        _ = new SurrogateCollection();
        var text = new TextObject("{=test}Supplies for {COMMANDER}");
        text.SetTextVariable("COMMANDER", new TextObject("Caladog"));
        var expected = new ArmyNeedsSuppliesAcceptance
        {
            ControllerId = "player-a",
            Grain = 70,
            Livestock = 7,
            Wine = 7,
            DueTime = new CampaignTime(987654321),
            Journal = new ArmyNeedsSuppliesJournal
            {
                Grain = 30,
                Entries = new[]
                {
                    new ArmyNeedsSuppliesJournal.Entry { Text = text, Time = new CampaignTime(12345) },
                },
            },
        };

        var actual = GenericAcceptFieldsSerializer.Deserialize<ArmyNeedsSuppliesAcceptance>(
            GenericAcceptFieldsSerializer.Serialize(expected));

        Assert.Equal("player-a", actual.ControllerId);
        Assert.Equal(70, actual.Grain);
        Assert.Equal(7, actual.Livestock);
        Assert.Equal(7, actual.Wine);
        Assert.Equal(987654321, actual.DueTime.NumTicks);
        Assert.Equal(30, actual.Journal.Grain);
        var entry = Assert.Single(actual.Journal.Entries);
        Assert.Equal(12345, entry.Time.NumTicks);
        TextObject restored = entry.Text;
        Assert.Equal(text.Value, restored.Value);
        Assert.Equal(text.ToString(), restored.ToString());
    }

    [Fact]
    public void ApplyingJournalTwiceDoesNotDuplicateLogsOrChangeAnotherQuest()
    {
        var quest = NewQuest();
        var otherQuest = NewQuest();
        var otherLog = new JournalLog(new CampaignTime(1), new TextObject("Other personal quest"));
        ((MBList<JournalLog>)otherQuest.JournalEntries).Add(otherLog);
        var journal = new ArmyNeedsSuppliesJournal
        {
            Grain = 20, Livestock = 2, Wine = 1,
            Entries = new[]
            {
                new ArmyNeedsSuppliesJournal.Entry { Text = new TextObject("accepted") },
                new ArmyNeedsSuppliesJournal.Entry { Text = TextObject.GetEmpty(), Task = new TextObject("grain"), Progress = 20, Range = 30, Type = LogType.Discreate },
                new ArmyNeedsSuppliesJournal.Entry { Text = TextObject.GetEmpty(), Task = new TextObject("livestock"), Progress = 2, Range = 3, Type = LogType.Discreate },
                new ArmyNeedsSuppliesJournal.Entry { Text = TextObject.GetEmpty(), Task = new TextObject("wine"), Progress = 1, Range = 3, Type = LogType.Discreate },
                new ArmyNeedsSuppliesJournal.Entry { Text = new TextObject("army disbanded") },
            },
        };

        journal.Apply(quest);
        journal.Apply(quest);

        Assert.Equal(5, quest.JournalEntries.Count);
        Assert.Equal("army disbanded", quest.JournalEntries[4].LogText.ToString());
        Assert.Equal(20, quest.JournalEntries[1].CurrentProgress);
        Assert.Equal(2, quest.JournalEntries[2].CurrentProgress);
        Assert.Equal(1, quest.JournalEntries[3].CurrentProgress);
        Assert.Same(otherLog, Assert.Single(otherQuest.JournalEntries));
    }

    private static Quest NewQuest()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        AccessTools.Field(typeof(QuestBase), "_journalEntries").SetValue(quest, new MBList<JournalLog>());
        return quest;
    }
}
