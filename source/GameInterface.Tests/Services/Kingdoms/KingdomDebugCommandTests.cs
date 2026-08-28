using Common;
using GameInterface.Services.Kingdoms.Commands;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

[Collection(ModInformationRoleCollection.Name)]
public class KingdomDebugCommandTests
{
    [Fact]
    public void ForceAlly_WhenClient_ReturnsServerOnlyError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var result = KingdomDebugCommand.ForceAlly(new List<string> { "kingdom1", "kingdom2" });

            Assert.Equal("Command is only available to run on the server", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ForceAlly_WithTooFewArgs_ReturnsUsage()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;

        try
        {
            var result = KingdomDebugCommand.ForceAlly(new List<string>());

            Assert.Equal(
                "Usage: coop.debug.kingdom.force_ally <kingdom1Id> <kingdom2Id> (run on the server)",
                result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ForceTradeAgreement_WhenClient_ReturnsServerOnlyError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var result = KingdomDebugCommand.ForceTradeAgreement(new List<string> { "kingdom1", "kingdom2" });

            Assert.Equal("Command is only available to run on the server", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ForceTradeAgreement_WithTooFewArgs_ReturnsUsage()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;

        try
        {
            var result = KingdomDebugCommand.ForceTradeAgreement(new List<string>());

            Assert.Equal(
                "Usage: coop.debug.kingdom.force_trade_agreement <kingdom1Id> <kingdom2Id> (run on the server)",
                result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
