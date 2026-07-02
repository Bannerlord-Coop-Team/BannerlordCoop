using Common;
using GameInterface.Services.Heroes.Commands;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

public class HeroDebugCommandTests
{
    [Fact]
    public void SetGold_WhenClient_ReturnsServerOnlyError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var result = HeroDebugCommand.SetGold(new List<string> { "Some Hero", "100" });

            Assert.Equal("The 'coop.debug.hero.SetGold' command cannot be used on the client. It is intended for server use only.", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}