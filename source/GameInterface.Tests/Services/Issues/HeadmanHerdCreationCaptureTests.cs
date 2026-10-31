using GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.IO;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Tests.Services.Issues;

public class HeadmanHerdCreationCaptureTests
{
    [Fact]
    public void Capture_PreservesTheServersDestinationRecipientAndAnimal()
    {
        var objects = new Mock<IObjectManager>();
        var issue = Empty<Issue>();
        issue._targetSettlement = Empty<Settlement>();
        issue._targetHero = Empty<Hero>();
        issue._herdTypeToDeliver = Empty<ItemObject>();
        var settlementId = "town_ES1";
        var heroId = "recipient";
        var itemId = "cow";
        objects.Setup(manager => manager.TryGetIdWithLogging(issue._targetSettlement, out settlementId)).Returns(true);
        objects.Setup(manager => manager.TryGetIdWithLogging(issue._targetHero, out heroId)).Returns(true);
        objects.Setup(manager => manager.TryGetIdWithLogging(issue._herdTypeToDeliver, out itemId)).Returns(true);

        var strategy = new HeadmanNeedsToDeliverAHerdCreationCaptureStrategy(objects.Object);
        using (var reader = new BinaryReader(new MemoryStream(strategy.Capture(issue))))
        {
            Assert.Equal(settlementId, reader.ReadString());
            Assert.Equal(heroId, reader.ReadString());
            Assert.Equal(itemId, reader.ReadString());
            Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        }
    }

    [Fact]
    public void Capture_RefusesAnUnregisteredDestination()
    {
        var strategy = new HeadmanNeedsToDeliverAHerdCreationCaptureStrategy(Mock.Of<IObjectManager>());
        Assert.Throws<InvalidOperationException>(() => strategy.Capture(Empty<Issue>()));
    }

    private static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
}
