using Coop.UI.LoadGameUI;
using TaleWorlds.SaveSystem;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>
/// Verifies the module marker used to identify saves that can be hosted.
/// </summary>
public class CoopSaveMetadataTests
{
    [Fact]
    public void ContainsCoopModule_ReturnsFalse_WhenMetadataIsNull()
    {
        Assert.False(CoopSaveMetadata.ContainsCoopModule(null));
    }

    [Fact]
    public void ContainsCoopModule_ReturnsFalse_WhenModuleListIsMissing()
    {
        Assert.False(CoopSaveMetadata.ContainsCoopModule(new MetaData()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Native;SandBoxCore;Sandbox;StoryMode")]
    [InlineData("Native;Cooperative;Sandbox")]
    [InlineData("Native;coop;Sandbox")]
    public void ContainsCoopModule_ReturnsFalse_WhenCoopModuleIsMissing(string modules)
    {
        var metadata = new MetaData();
        metadata.Add("Modules", modules);

        Assert.False(CoopSaveMetadata.ContainsCoopModule(metadata));
    }

    [Fact]
    public void ContainsCoopModule_ReturnsTrue_WhenCoopModuleIsPresent()
    {
        var metadata = new MetaData();
        metadata.Add("Modules", "Native;SandBoxCore;Sandbox;StoryMode;Coop");

        Assert.True(CoopSaveMetadata.ContainsCoopModule(metadata));
    }
}
