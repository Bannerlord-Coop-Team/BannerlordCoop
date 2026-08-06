using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Time.UI;
using Xunit;

namespace GameInterface.Tests.Services.Time.UI;

public class MissionMapTimeVMTests
{
    [Theory]
    [InlineData(TimeControlEnum.Pause, "Map Time: Paused")]
    [InlineData(TimeControlEnum.Play_1x, "Map Time: Normal")]
    [InlineData(TimeControlEnum.Play_2x, "Map Time: Fast Forward")]
    public void ConstructorSetsExpectedText(TimeControlEnum mode, string expectedText)
    {
        var viewModel = new MissionMapTimeVM(mode);
        
        Assert.Equal(expectedText, viewModel.MapTimeText);
    }

    [Fact]
    public void SetTimeControlMode_UpdatesText()
    {
        var viewModel = new MissionMapTimeVM(TimeControlEnum.Pause);
        
        viewModel.SetTimeControlMode(TimeControlEnum.Play_2x);
        
        Assert.Equal("Map Time: Fast Forward", viewModel.MapTimeText);
    }
}