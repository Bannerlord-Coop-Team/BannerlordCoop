using GameInterface.Services.Issues.Messages;
using GameInterface.Surrogates;
using ProtoBuf.Meta;
using System.IO;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkIssueRemovedSerializationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundTrip_PreservesFinalizationAndLocalizedJournal(bool hasTerminalLog)
    {
        _ = new SurrogateCollection();
        TextObject? log = null;
        if (hasTerminalLog)
        {
            log = new TextObject("{=quest_result}{GIVER} requested {COUNT} {ITEM}.");
            log.SetTextVariable("GIVER", new TextObject("{=giver_name}Artisan"));
            log.SetTextVariable("COUNT", 12);
            log.SetTextVariable("ITEM", "iron");
        }
        var original = new NetworkIssueRemoved("giver-1", IssueFinalizeReason.QuestFail,
            proof: 2, localConsequenceDeferred: hasTerminalLog, terminalLog: log);

        using var stream = new MemoryStream();
        RuntimeTypeModel.Default.Serialize(stream, original);
        Assert.True(stream.Length > 0);
        stream.Position = 0;
        var result = (NetworkIssueRemoved)RuntimeTypeModel.Default.Deserialize(stream, null, typeof(NetworkIssueRemoved));

        Assert.Equal(original.OwnerId, result.OwnerId);
        Assert.Equal(original.Reason, result.Reason);
        Assert.Equal(original.Proof, result.Proof);
        Assert.Equal(original.LocalConsequenceDeferred, result.LocalConsequenceDeferred);
        if (hasTerminalLog)
        {
            Assert.Equal(log!.Value, result.TerminalLog.Value);
            Assert.Equal("{=giver_name}Artisan", Assert.IsType<TextObject>(result.TerminalLog.Attributes["GIVER"]).Value);
            Assert.Equal(12, Assert.IsType<int>(result.TerminalLog.Attributes["COUNT"]));
            Assert.Equal("iron", Assert.IsType<string>(result.TerminalLog.Attributes["ITEM"]));
        }
        else
        {
            Assert.Null(result.TerminalLog);
        }
    }
}
