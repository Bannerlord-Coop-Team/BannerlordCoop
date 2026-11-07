using GameInterface.Services.Issues.Quests.VillageNeedsTools;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Moq;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;

namespace GameInterface.Tests.Services.Issues;

public class VillageNeedsToolsCreationCaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Capture_PreservesTheRequestedQuantityAndThePromisedReward(bool exchange)
    {
        var tools = (ItemObject)RuntimeHelpers.GetUninitializedObject(typeof(ItemObject));
        var grain = (ItemObject)RuntimeHelpers.GetUninitializedObject(typeof(ItemObject));
        var issue = (Issue)RuntimeHelpers.GetUninitializedObject(typeof(Issue));
        AccessTools.Field(typeof(Issue), nameof(Issue._requestedItem)).SetValue(issue, tools);
        AccessTools.Field(typeof(Issue), nameof(Issue._exchangeItem)).SetValue(issue, exchange ? grain : null);
        AccessTools.Field(typeof(Issue), nameof(Issue._numberOfRequestedItem)).SetValue(issue, 11);
        AccessTools.Field(typeof(Issue), nameof(Issue._numberOfExchangeItem)).SetValue(issue, exchange ? 23 : 0);
        AccessTools.Field(typeof(Issue), nameof(Issue._payment)).SetValue(issue, exchange ? 0 : 1460);

        var objects = new Mock<IObjectManager>();
        var toolsId = "tools";
        var grainId = "grain";
        objects.Setup(manager => manager.TryGetIdWithLogging(tools, out toolsId)).Returns(true);
        objects.Setup(manager => manager.TryGetIdWithLogging(grain, out grainId)).Returns(true);

        var strategy = new VillageNeedsToolsCreationCaptureStrategy(objects.Object);
        using var reader = new BinaryReader(new MemoryStream(strategy.Capture(issue)));

        Assert.Equal("tools", reader.ReadString());
        Assert.Equal(exchange ? "grain" : string.Empty, reader.ReadString());
        Assert.Equal(11, reader.ReadInt32());
        Assert.Equal(exchange ? 23 : 0, reader.ReadInt32());
        Assert.Equal(exchange ? 0 : 1460, reader.ReadInt32());
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Fact]
    public void Capture_RejectsAnOfferWhoseItemHasNoNetworkIdentity()
    {
        var issue = (Issue)RuntimeHelpers.GetUninitializedObject(typeof(Issue));
        var strategy = new VillageNeedsToolsCreationCaptureStrategy(new Mock<IObjectManager>().Object);

        Assert.Throws<InvalidOperationException>(() => strategy.Capture(issue));
    }
}
