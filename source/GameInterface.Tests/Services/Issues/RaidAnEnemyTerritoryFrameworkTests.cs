using GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;
using GameInterface.Services.ObjectManager;
using Moq;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class RaidAnEnemyTerritoryFrameworkTests
{
    [Fact]
    public void CreationRejectsATruncatedKingdomBeforeConstructingAnIssue()
    {
        var strategy = new RaidAnEnemyTerritoryCreationCaptureStrategy(Mock.Of<IObjectManager>());
        Assert.Throws<InvalidDataException>(() => strategy.CreateIssue(null, new byte[] { 3, 65 }));
    }
}
