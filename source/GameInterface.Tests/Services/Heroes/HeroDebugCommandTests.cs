using GameInterface.Services.Heroes.Commands;
using GameInterface.Tests;
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

}
