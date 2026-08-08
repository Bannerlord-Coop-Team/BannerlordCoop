using Common;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Tests;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

[Collection(ModInformationRoleCollection.Name)]
public class HeroDebugCommandTests
{
    [Fact]
    public void NameStartsWithPrefix_NoPrefix_Matches()
    {
        Assert.True(HeroDebugCommand.NameStartsWithPrefix("Lady Isolla", string.Empty));
    }

    [Theory]
    [InlineData("LADY", true)]
    [InlineData("lady iso", true)]
    [InlineData("Isolla", false)]
    public void NameStartsWithPrefix_FiltersCaseInsensitively(
        string prefix,
        bool expected)
    {
        Assert.Equal(expected, HeroDebugCommand.NameStartsWithPrefix("Lady Isolla", prefix));
    }

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
