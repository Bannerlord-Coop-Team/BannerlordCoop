#if DEBUG
using Common;
using GameInterface.Services.Heroes;
using GameInterface.Services.Save.Patches;
using Moq;
using System;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using Xunit;

namespace GameInterface.Tests.Services.Save;

[Collection(ModInformationRoleCollection.Name)]
public sealed class NavalLabSaveIsolationTests : IDisposable
{
    private readonly string previous = ModInformation.NavalLabCapability;
    private readonly bool previousRole = ModInformation.IsServer;

    public NavalLabSaveIsolationTests()
    {
        ModInformation.ConfigureNavalLab("new-campaign:571cda18-4f3b-4787-9ac0-5997f17f083e", "save-regression", true);
        ModInformation.IsServer = true;
    }

    [Theory]
    [InlineData("MP")]
    [InlineData("default_new_game")]
    [InlineData("auto_save_1")]
    public void DiskSaveAndRetry_AreDeniedWithFailureCallbackBeforeDriverUse(string saveName)
    {
        var driver = new Mock<ISaveDriver>(MockBehavior.Strict);
        int completions = 0;
        for (int i = 0; i < 2; i++)
        {
            Assert.False(SavePatches.Prefix(null!, ref saveName, driver.Object, result =>
            {
                Assert.Equal(SaveResult.GeneralFailure, result);
                completions++;
            }));
        }
        Assert.Equal(2, completions);
        driver.VerifyNoOtherCalls();
        Assert.False(SaveHandlerClientBlockPatch.Prefix());
    }

    [Fact]
    public void TransferSave_KeepsOriginalDriverAndCompletionOwnedByVanilla()
    {
        string name = "TransferSave";
        var driver = new CoopInMemSaveDriver();
        Assert.True(SavePatches.Prefix(null!, ref name, driver, _ => throw new Exception("premature completion")));
        Assert.Equal("TransferSave", name);
        Assert.Null(driver.Data);
    }

    [Fact]
    public void NormalServerSaveAndClientBlock_AreUnchangedWithoutOptIn()
    {
        typeof(ModInformation).GetProperty(nameof(ModInformation.NavalLabCapability))!.SetValue(null, null);
        string name = "MP";
        var driver = new Mock<ISaveDriver>(MockBehavior.Strict);
        Assert.True(SavePatches.Prefix(null!, ref name, driver.Object, _ => throw new Exception("premature completion")));
        Assert.True(SaveHandlerClientBlockPatch.Prefix());
        ModInformation.IsServer = false;
        Assert.False(SaveHandlerClientBlockPatch.Prefix());
        driver.VerifyNoOtherCalls();
    }

    public void Dispose()
    {
        typeof(ModInformation).GetProperty(nameof(ModInformation.NavalLabCapability))!.SetValue(null, previous);
        ModInformation.IsServer = previousRole;
    }
}
#endif
