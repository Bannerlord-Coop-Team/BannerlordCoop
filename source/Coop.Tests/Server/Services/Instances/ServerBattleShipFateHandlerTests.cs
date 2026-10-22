using Coop.Core.Server.Services.Instances.Handlers;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

/// <summary>M6 D4: which sender the server lets report a ship's fate.</summary>
public class ServerBattleShipFateHandlerTests
{
    [Theory]
    // A player reports its own party's ships, whatever the host epoch.
    [InlineData(true, true, true, true, false, null)]
    // The battle host at the current epoch reports the AI parties' ships.
    [InlineData(false, true, true, false, true, null)]
    [InlineData(false, true, true, false, false, "the sender is not the battle host at this epoch")]
    [InlineData(false, true, true, true, true, "another player owns it")]
    [InlineData(false, true, false, false, true, "its owner is not in the battle")]
    [InlineData(false, false, false, false, true, "the ship has no owner")]
    public void RejectionOf_LetsAPlayerReportItsShipsAndTheHostTheAiShips(bool ownedBySender, bool hasOwner, bool ownerInBattle,
        bool ownedByPlayer, bool isHostAtEpoch, string expected)
    {
        Assert.Equal(expected, ServerBattleShipFateHandler.RejectionOf(ownedBySender, hasOwner, ownerInBattle, ownedByPlayer,
            isHostAtEpoch));
    }
}
